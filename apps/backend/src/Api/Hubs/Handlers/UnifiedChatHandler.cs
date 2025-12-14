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
/// Unified chat handler that supports both text and multimodal content
/// with consistent thread-based persistence and memory management.
/// </summary>
public class UnifiedChatHandler(
    IMasterOrchestrator orchestrator,
    AudioTranscriptionService audioService,
    IServiceScopeFactory scopeFactory,
    ILogger<UnifiedChatHandler> logger
) : IUnifiedChatHandler
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public async IAsyncEnumerable<MasterStreamingResponse> StreamAsync(
        UnifiedThreadedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // Validate request
        if (!request.HasContent)
        {
            yield return CreateErrorResponse("Request must include a message or content items");
            yield break;
        }

        // Validate multimodal content if present
        if (request.Contents?.Count > 0)
        {
            foreach (var content in request.Contents)
            {
                var (isValid, errorMessage) = ContentConverter.ValidateContentInput(content);
                if (!isValid)
                {
                    yield return CreateErrorResponse(errorMessage ?? "Invalid content");
                    yield break;
                }
            }
        }

        // Process contents and build AI content list
        var (aiContents, contentDescriptions, transcription) = await ProcessAllContentsAsync(
            request,
            cancellationToken
        );

        // Send any transcription responses
        await foreach (
            var transcriptionResponse in transcription.WithCancellation(cancellationToken)
        )
        {
            yield return transcriptionResponse;
        }

        // Initialize thread for persistence
        var threadContext = await InitializeThreadAsync(
            request.ThreadId,
            contentDescriptions,
            request.Contents,
            request.Message,
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

        // Build unified request for orchestrator
        var conversationId = request.ConversationId ?? threadContext.ThreadId.ToString();
        var unifiedRequest = new UnifiedChatRequest
        {
            ConversationId = conversationId,
            Message = request.Message,
            Contents = aiContents.Count > 0 ? aiContents : null,
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
                yield return MapChunkToStreamingResponse(chunk);

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

    private async Task<(
        List<AIContent> aiContents,
        List<string> descriptions,
        IAsyncEnumerable<MasterStreamingResponse> transcriptions
    )> ProcessAllContentsAsync(
        UnifiedThreadedChatRequest request,
        CancellationToken cancellationToken
    )
    {
        var aiContents = new List<AIContent>();
        var descriptions = new List<string>();
        var transcriptionResponses = new List<MasterStreamingResponse>();

        // Add text message if present
        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            aiContents.Add(new TextContent(request.Message));
            descriptions.Add(request.Message);
        }

        // Process multimodal contents if present
        if (request.Contents?.Count > 0)
        {
            foreach (var content in request.Contents)
            {
                if (IsAudioContent(content))
                {
                    var (aiContent, response, description) = await ProcessAudioContentAsync(
                        content
                    );
                    if (aiContent != null)
                        aiContents.Add(aiContent);
                    if (response != null)
                        transcriptionResponses.Add(response);
                    if (!string.IsNullOrEmpty(description))
                        descriptions.Add(description);
                }
                else
                {
                    var aiContent = ContentConverter.ConvertToAIContent(content);
                    if (aiContent != null)
                    {
                        aiContents.Add(aiContent);
                        var description = GenerateContentDescription(content);
                        if (!string.IsNullOrEmpty(description))
                            descriptions.Add(description);
                    }
                }
            }
        }

        return (aiContents, descriptions, ToAsyncEnumerable(transcriptionResponses));
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }

    private async Task<ThreadContext> InitializeThreadAsync(
        string? threadIdStr,
        List<string> contentDescriptions,
        List<ContentInput>? contents,
        string? textMessage,
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
            var threadTitle = GenerateThreadTitle(contentDescriptions, contents, textMessage);
            var thread = await unitOfWork.Threads.CreateAsync(
                new ChatThread { Title = threadTitle },
                cancellationToken
            );
            actualThreadId = thread.Id;
        }

        // Save user message with attachments
        var userMessageContent = BuildUserMessageContent(
            contentDescriptions,
            contents,
            textMessage
        );
        var attachmentsJson = BuildAttachmentsJson(contents);

        await unitOfWork.Messages.AddAsync(
            new Models.ChatMessage
            {
                ThreadId = actualThreadId,
                Role = MessageRole.User,
                Content = userMessageContent,
                AttachmentsJson = attachmentsJson,
            },
            cancellationToken
        );

        logger.LogInformation(
            "User message saved to thread {ThreadId}: ContentPreview={ContentPreview}, HasAttachments={HasAttachments}",
            actualThreadId,
            userMessageContent.Length > 100
                ? userMessageContent[..100] + "..."
                : userMessageContent,
            !string.IsNullOrEmpty(attachmentsJson)
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
                Attachments = DeserializeAttachments(m.AttachmentsJson),
            })
            .ToList();
    }

    /// <summary>
    /// Build JSON representation of multimodal attachments for database storage
    /// </summary>
    private static string? BuildAttachmentsJson(List<ContentInput>? contents)
    {
        if (contents == null || contents.Count == 0)
            return null;

        var attachments = contents
            .Where(c => c.Type?.ToLowerInvariant() != "text")
            .Select(c => new SerializableAttachment
            {
                Type = c.Type ?? "unknown",
                MediaType = c.MediaType ?? "application/octet-stream",
                Data = c.Data, // Base64 image data
                Url = c.Uri, // ContentInput uses "Uri", SerializableAttachment uses "Url"
                FileName = c.FileName,
                Transcription = null, // Audio transcriptions are stored separately in content
            })
            .ToList();

        if (attachments.Count == 0)
            return null;

        return JsonSerializer.Serialize(attachments, CamelCaseOptions);
    }

    /// <summary>
    /// Deserialize attachments from JSON storage
    /// </summary>
    private static List<SerializableAttachment>? DeserializeAttachments(string? attachmentsJson)
    {
        if (string.IsNullOrEmpty(attachmentsJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<SerializableAttachment>>(
                attachmentsJson,
                CamelCaseOptions
            );
        }
        catch
        {
            return null;
        }
    }

    private static string BuildUserMessageContent(
        List<string> descriptions,
        List<ContentInput>? contents,
        string? textMessage
    )
    {
        var sb = new StringBuilder();

        // Add main text message first if present
        if (!string.IsNullOrWhiteSpace(textMessage))
        {
            sb.AppendLine(textMessage);
        }

        // Add other text descriptions (excluding main message to avoid duplication)
        foreach (var desc in descriptions)
        {
            if (!string.IsNullOrWhiteSpace(desc) && desc != textMessage)
            {
                sb.AppendLine(desc);
            }
        }

        // Add attachment summaries for non-text content
        var attachments = contents?.Where(c => c.Type?.ToLowerInvariant() != "text").ToList();
        if (attachments?.Count > 0)
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

    private static string GenerateThreadTitle(
        List<string> descriptions,
        List<ContentInput>? contents,
        string? textMessage
    )
    {
        // Try text message first
        if (!string.IsNullOrWhiteSpace(textMessage))
        {
            return TruncateTitle(textMessage);
        }

        // Try other descriptions
        var textContent = descriptions.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
        if (!string.IsNullOrEmpty(textContent))
        {
            return TruncateTitle(textContent);
        }

        // Fallback to describing attachments
        var attachmentTypes = contents
            ?.Where(c => c.Type?.ToLowerInvariant() != "text")
            .Select(c => c.Type?.ToLowerInvariant() ?? "file")
            .Distinct()
            .ToList();

        if (attachmentTypes?.Count > 0)
        {
            return $"Multi-modal: {string.Join(", ", attachmentTypes)}";
        }

        return "New Conversation";
    }

    private static string TruncateTitle(string content)
    {
        var title = content.Length > 50 ? content[..50] + "..." : content;
        var firstSentenceEnd = title.IndexOfAny(['.', '?', '!', '\n']);
        if (firstSentenceEnd > 0 && firstSentenceEnd < title.Length - 3)
            title = title[..(firstSentenceEnd + 1)];
        return title.Trim();
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
                        new { projectionResult = collector.ProjectionResult },
                        CamelCaseOptions
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

    private async Task<(
        AIContent? content,
        MasterStreamingResponse? response,
        string? description
    )> ProcessAudioContentAsync(ContentInput content)
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

        return (textContent, transcriptionResponse, description);
    }

    private static string ExtractBase64Data(string data)
    {
        if (!data.StartsWith("data:"))
            return data;

        var commaIndex = data.IndexOf(',');
        return commaIndex >= 0 ? data[(commaIndex + 1)..] : data;
    }

    private static MasterStreamingResponse CreateErrorResponse(string message) =>
        new()
        {
            Type = ResponseType.Error.ToString(),
            Content = message,
            IsComplete = true,
        };

    /// <summary>
    /// Maps an orchestrator streaming chunk to a SignalR response.
    /// </summary>
    internal static MasterStreamingResponse MapChunkToStreamingResponse(
        UnifiedStreamingChunk chunk
    ) =>
        new()
        {
            Type = chunk.Type.ToString(),
            Content = chunk.Content,
            SubAgentName = chunk.SubAgentName,
            ToolName = chunk.ToolName,
            IsComplete = chunk.Type == StreamingChunkType.Complete,
            Metadata = chunk.Metadata,
            StepId = chunk.StepId,
            StepName = chunk.StepName,
            StepNameAr = chunk.StepNameAr,
            StepNumber = chunk.StepNumber,
            TotalSteps = chunk.TotalSteps,
            StepCompleted = chunk.Type == StreamingChunkType.StepComplete,
            StepDurationMs = chunk.StepDurationMs,
            StepDetails = chunk.StepDetails,
            ProjectionResult = chunk.FinalResult?.ProjectionResult,
        };

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
