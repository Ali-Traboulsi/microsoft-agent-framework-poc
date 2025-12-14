using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Helpers;
using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Models;
using AgentFrameworkQuickStart.Services;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Hubs.Handlers;

/// <summary>
/// Handles multi-modal chat streaming with support for images, audio, and documents
/// </summary>
public class MultiModalChatHandler(
    IMasterOrchestrator orchestrator,
    AudioTranscriptionService audioService,
    IServiceScopeFactory scopeFactory,
    ILogger<MultiModalChatHandler> logger
) : IMultiModalChatHandler
{
    public async IAsyncEnumerable<MasterStreamingResponse> StreamAsync(
        MultiModalChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // Validate request
        var validationResult = ValidateRequest(request);
        if (validationResult != null)
        {
            yield return validationResult;
            yield break;
        }

        // Process contents and build AI content list
        var aiContents = new List<AIContent>();
        var contentDescriptions = new List<string>();

        if (!string.IsNullOrEmpty(request.Message))
        {
            aiContents.Add(new TextContent(request.Message));
            contentDescriptions.Add(request.Message);
        }

        await foreach (var result in ProcessContentsAsync(request.Contents!, cancellationToken))
        {
            if (result.Response != null)
                yield return result.Response;

            if (result.Content != null)
            {
                aiContents.Add(result.Content);
                if (!string.IsNullOrEmpty(result.Description))
                    contentDescriptions.Add(result.Description);
            }
        }

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();

        // Initialize thread for persistence
        var threadContext = await InitializeThreadAsync(
            request.ThreadId,
            contentDescriptions,
            request.Contents!,
            cancellationToken
        );

        // Send thread ID to client
        yield return new MasterStreamingResponse
        {
            Type = "ThreadCreated",
            Content = threadContext.ThreadId.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["threadId"] = threadContext.ThreadId.ToString(),
            },
            IsComplete = false,
        };

        var unifiedRequest = new UnifiedChatRequest
        {
            ConversationId = conversationId,
            Message = request.Message,
            Contents = aiContents,
            PriorMessages =
                threadContext.PriorMessages.Count > 0 ? threadContext.PriorMessages : null,
            EnableThinking = request.EnableThinking,
            CancellationToken = cancellationToken,
        };

        var responseCollector = new ResponseCollector();
        var messageSaved = false;

        try
        {
            await foreach (
                var chunk in orchestrator.ProcessAsync(unifiedRequest, cancellationToken)
            )
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                responseCollector.Collect(chunk);
                yield return ChatStreamHandler.MapChunkToStreamingResponse(chunk);

                if (chunk.Type == StreamingChunkType.Complete)
                {
                    await SaveAssistantMessageAsync(
                        threadContext.ThreadId,
                        responseCollector,
                        cancellationToken
                    );
                    messageSaved = true;
                }
            }
        }
        finally
        {
            // Ensure assistant message is saved even if stream was interrupted
            if (!messageSaved && responseCollector.HasContent)
            {
                logger.LogWarning(
                    "Stream ended without Complete response, saving partial message to thread {ThreadId}",
                    threadContext.ThreadId
                );

                await SaveAssistantMessageAsync(
                    threadContext.ThreadId,
                    responseCollector,
                    CancellationToken.None
                );
            }
        }
    }

    private async Task<ThreadContext> InitializeThreadAsync(
        string? threadIdStr,
        List<string> contentDescriptions,
        List<ContentInput> contents,
        CancellationToken cancellationToken
    )
    {
        Guid? parsedThreadId = null;
        if (!string.IsNullOrEmpty(threadIdStr) && Guid.TryParse(threadIdStr, out var parsed))
            parsedThreadId = parsed;

        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Guid actualThreadId;
        var priorMessages = new List<ConversationMessage>();

        if (parsedThreadId.HasValue && parsedThreadId.Value != Guid.Empty)
        {
            actualThreadId = parsedThreadId.Value;
            priorMessages = await LoadPriorMessagesAsync(
                unitOfWork,
                actualThreadId,
                cancellationToken
            );
        }
        else
        {
            var threadTitle = GenerateMultiModalThreadTitle(contentDescriptions, contents);
            var thread = await unitOfWork.Threads.CreateAsync(
                new ChatThread { Title = threadTitle },
                cancellationToken
            );
            actualThreadId = thread.Id;
        }

        // Save user message with multi-modal content description
        var userMessageContent = BuildUserMessageContent(contentDescriptions, contents);
        await unitOfWork.Messages.AddAsync(
            new Models.ChatMessage
            {
                ThreadId = actualThreadId,
                Role = MessageRole.User,
                Content = userMessageContent,
            },
            cancellationToken
        );

        logger.LogInformation(
            "Multi-modal user message saved to thread {ThreadId}: {ContentPreview}",
            actualThreadId,
            userMessageContent.Length > 100 ? userMessageContent[..100] + "..." : userMessageContent
        );

        return new ThreadContext(actualThreadId, priorMessages);
    }

    private static async Task<List<ConversationMessage>> LoadPriorMessagesAsync(
        IUnitOfWork unitOfWork,
        Guid threadId,
        CancellationToken cancellationToken
    )
    {
        var existingThread = await unitOfWork.Threads.GetByIdWithMessagesAsync(
            threadId,
            cancellationToken
        );

        if (existingThread?.Messages == null || existingThread.Messages.Count == 0)
            return [];

        return existingThread
            .Messages.OrderBy(m => m.SequenceNumber)
            .Select(m => new ConversationMessage
            {
                Role = m.Role.ToString().ToLower(),
                Content = m.Content,
                SubAgentName = m.SubAgentName,
            })
            .ToList();
    }

    private static string BuildUserMessageContent(
        List<string> descriptions,
        List<ContentInput> contents
    )
    {
        var sb = new StringBuilder();

        // Add text descriptions
        foreach (var desc in descriptions)
        {
            if (!string.IsNullOrWhiteSpace(desc))
            {
                sb.AppendLine(desc);
            }
        }

        // Add attachment summaries
        var attachments = contents.Where(c => c.Type?.ToLowerInvariant() != "text").ToList();
        if (attachments.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("[Attachments:]");
            foreach (var attachment in attachments)
            {
                var type = attachment.Type?.ToLowerInvariant() ?? "unknown";
                var fileName = attachment.FileName ?? "unnamed";
                var mediaType = attachment.MediaType ?? "unknown";
                sb.AppendLine($"- {type}: {fileName} ({mediaType})");
            }
        }

        return sb.ToString().Trim();
    }

    private static string GenerateMultiModalThreadTitle(
        List<string> descriptions,
        List<ContentInput> contents
    )
    {
        // Try to use text content first
        var textContent = descriptions.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
        if (!string.IsNullOrEmpty(textContent))
        {
            var title = textContent.Length > 50 ? textContent[..50] + "..." : textContent;
            var firstSentenceEnd = title.IndexOfAny(['.', '?', '!', '\n']);
            if (firstSentenceEnd > 0 && firstSentenceEnd < title.Length - 3)
                title = title[..(firstSentenceEnd + 1)];
            return title.Trim();
        }

        // Fallback to describing attachments
        var attachmentTypes = contents
            .Where(c => c.Type?.ToLowerInvariant() != "text")
            .Select(c => c.Type?.ToLowerInvariant() ?? "file")
            .Distinct()
            .ToList();

        if (attachmentTypes.Count > 0)
        {
            return $"Multi-modal: {string.Join(", ", attachmentTypes)}";
        }

        return "Multi-modal conversation";
    }

    private async Task SaveAssistantMessageAsync(
        Guid threadId,
        ResponseCollector collector,
        CancellationToken cancellationToken
    )
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        logger.LogInformation(
            "Saving assistant message to thread {ThreadId}: ContentLength={ContentLength}, HasProjection={HasProjection}",
            threadId,
            collector.Content.Length,
            collector.ProjectionResult != null
        );

        var assistantMessage = new Models.ChatMessage
        {
            ThreadId = threadId,
            Role = MessageRole.Assistant,
            Content = collector.Content,
            SubAgentName = collector.SubAgentName,
            MetadataJson =
                collector.ProjectionResult != null
                    ? JsonSerializer.Serialize(
                        new { projectionResult = collector.ProjectionResult }
                    )
                    : null,
        };

        await unitOfWork.Messages.AddAsync(assistantMessage, cancellationToken);

        logger.LogInformation(
            "Assistant message saved to thread {ThreadId} with ID {MessageId}",
            threadId,
            assistantMessage.Id
        );
    }

    private static MasterStreamingResponse? ValidateRequest(MultiModalChatRequest request)
    {
        if (request.Contents == null || request.Contents.Count == 0)
        {
            return new MasterStreamingResponse
            {
                Type = ResponseType.Error.ToString(),
                Content = "Request must include at least one content item",
                IsComplete = true,
            };
        }

        foreach (var content in request.Contents)
        {
            var (isValid, errorMessage) = ContentConverter.ValidateContentInput(content);
            if (!isValid)
            {
                return new MasterStreamingResponse
                {
                    Type = ResponseType.Error.ToString(),
                    Content = errorMessage,
                    IsComplete = true,
                };
            }
        }

        return null;
    }

    private async IAsyncEnumerable<ContentProcessResult> ProcessContentsAsync(
        List<ContentInput> contents,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        foreach (var content in contents)
        {
            if (IsAudioContent(content))
            {
                var result = await ProcessAudioContentAsync(content);
                yield return result;
            }
            else
            {
                var aiContent = ContentConverter.ConvertToAIContent(content);
                if (aiContent != null)
                {
                    var description = GenerateContentDescription(content);
                    yield return new ContentProcessResult(aiContent, Description: description);
                }
            }
        }
    }

    private static string? GenerateContentDescription(ContentInput content)
    {
        var type = content.Type?.ToLowerInvariant();
        return type switch
        {
            "text" => content.Text,
            "image" => $"[Image: {content.FileName ?? "image"}]",
            "audio" => $"[Audio: {content.FileName ?? "audio"}]",
            "uri" => $"[Link: {content.Uri}]",
            "pdf" => $"[PDF: {content.FileName ?? "document.pdf"}]",
            _ => null,
        };
    }

    private static bool IsAudioContent(ContentInput content) =>
        content.Type?.ToLowerInvariant() == "audio"
        && !string.IsNullOrEmpty(content.Data)
        && !string.IsNullOrEmpty(content.MediaType)
        && AudioTranscriptionService.IsAudioFile(content.MediaType);

    private async Task<ContentProcessResult> ProcessAudioContentAsync(ContentInput content)
    {
        var base64Data = ExtractBase64Data(content.Data!);
        var audioBytes = Convert.FromBase64String(base64Data);
        var fileName = content.FileName ?? "audio.mp3";

        var transcript = await audioService.TranscribeAudioAsync(audioBytes, fileName);

        var transcriptionResponse = new MasterStreamingResponse
        {
            Type = ResponseType.Transcription.ToString(),
            Content = transcript,
            Metadata = new Dictionary<string, object>
            {
                ["FileName"] = fileName,
                ["FileSize"] = audioBytes.Length,
                ["MediaType"] = content.MediaType ?? "audio/unknown",
            },
            IsComplete = false,
        };

        var textContent = new TextContent($"[Audio file '{fileName}' transcription]: {transcript}");
        var description = $"[Audio transcription '{fileName}']: {transcript}";

        return new ContentProcessResult(textContent, transcriptionResponse, description);
    }

    private static string ExtractBase64Data(string data)
    {
        if (!data.StartsWith("data:"))
            return data;

        var commaIndex = data.IndexOf(',');
        return commaIndex >= 0 ? data[(commaIndex + 1)..] : data;
    }

    private record ContentProcessResult(
        AIContent? Content,
        MasterStreamingResponse? Response = null,
        string? Description = null
    );

    private record ThreadContext(Guid ThreadId, List<ConversationMessage> PriorMessages);

    private class ResponseCollector
    {
        private readonly StringBuilder _content = new();

        public string? SubAgentName { get; private set; }
        public object? ProjectionResult { get; private set; }
        public string Content => _content.ToString();
        public bool HasContent => _content.Length > 0 || ProjectionResult != null;

        public void Collect(UnifiedStreamingChunk chunk)
        {
            if (
                !string.IsNullOrEmpty(chunk.Content)
                && (
                    chunk.Type == StreamingChunkType.Content
                    || chunk.Type == StreamingChunkType.Progress
                )
            )
            {
                _content.Append(chunk.Content);
            }

            if (!string.IsNullOrEmpty(chunk.SubAgentName))
                SubAgentName = chunk.SubAgentName;

            if (chunk.FinalResult?.ProjectionResult != null)
                ProjectionResult = chunk.FinalResult.ProjectionResult;
        }
    }
}
