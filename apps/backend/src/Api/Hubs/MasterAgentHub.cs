using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Helpers;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;
using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Models;
using AgentFrameworkQuickStart.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Hubs;

/// <summary>
/// SignalR hub for master orchestrator with streaming support
/// </summary>
/// <remarks>
/// Note: SignalR does not support multipart/form-data file uploads.
/// For file uploads with streaming, use ChatStreamMultiModal with base64-encoded data.
/// For direct file uploads without streaming, use the HTTP endpoint /chat/multimodal/upload
/// </remarks>
public class MasterAgentHub : Hub
{
    private readonly IMasterOrchestrator _orchestrator;
    private readonly ILogger<MasterAgentHub> _logger;
    private readonly AudioTranscriptionService _audioService;
    private readonly IServiceScopeFactory _scopeFactory;

    public MasterAgentHub(
        IMasterOrchestrator orchestrator,
        ILogger<MasterAgentHub> logger,
        AudioTranscriptionService audioService,
        IServiceScopeFactory scopeFactory
    )
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _audioService = audioService;
        _scopeFactory = scopeFactory;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Master agent client connected: {ConnectionId}",
            Context.ConnectionId
        );
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "Master agent client disconnected: {ConnectionId}, Exception: {Exception}",
            Context.ConnectionId,
            exception?.Message
        );
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Stream chat responses from the master orchestrator
    /// </summary>
    public async IAsyncEnumerable<MasterStreamingResponse> ChatStream(
        string message,
        string conversationId,
        bool enableThinking = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Master agent streaming chat for {ConnectionId}, Conversation: {ConversationId}",
            Context.ConnectionId,
            conversationId
        );

        await foreach (
            var response in _orchestrator
                .ProcessRequestStreamingAsync(message, conversationId, enableThinking)
                .WithCancellation(cancellationToken)
        )
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Master agent streaming cancelled for {ConnectionId}",
                    Context.ConnectionId
                );
                break;
            }

            yield return new MasterStreamingResponse
            {
                Type = response.Type.ToString(),
                Content = response.Content,
                SubAgentName = response.SubAgentName,
                ToolName = response.ToolName,
                IsComplete = response.Type == ResponseType.Complete,
                Metadata = response.Metadata,
                // Progress step fields
                StepId = response.StepId,
                StepName = response.StepName,
                StepNameAr = response.StepNameAr,
                StepNumber = response.StepNumber,
                TotalSteps = response.TotalSteps,
                StepCompleted = response.Type == ResponseType.StepComplete,
                StepDurationMs = response.StepDurationMs,
                StepDetails = response.StepDetails,
                // Projection result (included in Complete response)
                ProjectionResult = response.ProjectionResult,
            };
        }

        _logger.LogInformation(
            "Master agent streaming completed for {ConnectionId}",
            Context.ConnectionId
        );
    }

    /// <summary>
    /// Stream multi-modal chat responses from the master orchestrator
    /// </summary>
    /// <param name="request">Multi-modal chat request containing text and/or media content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async IAsyncEnumerable<MasterStreamingResponse> ChatStreamMultiModal(
        MultiModalChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "Master agent multi-modal streaming chat for {ConnectionId}, Conversation: {ConversationId}, ContentCount: {ContentCount}",
            Context.ConnectionId,
            request.ConversationId ?? "new",
            request.Contents?.Count ?? 0
        );

        // Validate request
        if (request.Contents == null || request.Contents.Count == 0)
        {
            yield return new MasterStreamingResponse
            {
                Type = ResponseType.Error.ToString(),
                Content = "Request must include at least one content item",
                IsComplete = true,
            };
            yield break;
        }

        // Validate each content input
        foreach (var content in request.Contents)
        {
            var (isValid, errorMessage) = ContentConverter.ValidateContentInput(content);
            if (!isValid)
            {
                yield return new MasterStreamingResponse
                {
                    Type = ResponseType.Error.ToString(),
                    Content = errorMessage,
                    IsComplete = true,
                };
                yield break;
            }
        }

        // Convert to AIContent list for native multimodal support
        var aiContents = new List<AIContent>();

        // Add text message first if provided
        if (!string.IsNullOrEmpty(request.Message))
        {
            aiContents.Add(new TextContent(request.Message));
        }

        // Process each content item - transcribe audio, pass everything else to AI natively
        foreach (var content in request.Contents)
        {
            _logger.LogInformation(
                "Processing content: Type={Type}, HasData={HasData}, HasText={HasText}, MediaType={MediaType}",
                content.Type,
                !string.IsNullOrEmpty(content.Data),
                !string.IsNullOrEmpty(content.Text),
                content.MediaType
            );

            if (
                content.Type?.ToLowerInvariant() == "audio"
                && !string.IsNullOrEmpty(content.Data)
                && !string.IsNullOrEmpty(content.MediaType)
                && AudioTranscriptionService.IsAudioFile(content.MediaType)
            )
            {
                _logger.LogInformation("Audio content detected, starting transcription...");

                // Extract base64 audio data
                var base64Data = content.Data;
                if (base64Data.StartsWith("data:"))
                {
                    var commaIndex = base64Data.IndexOf(',');
                    if (commaIndex >= 0)
                    {
                        base64Data = base64Data.Substring(commaIndex + 1);
                    }
                }

                // Convert to bytes and transcribe
                var audioBytes = Convert.FromBase64String(base64Data);
                var fileName = content.FileName ?? "audio.mp3";

                _logger.LogInformation(
                    "Transcribing audio file {FileName} ({Size} bytes)",
                    fileName,
                    audioBytes.Length
                );

                var transcript = await _audioService.TranscribeAudioAsync(audioBytes, fileName);

                _logger.LogInformation(
                    "Transcription complete: {Transcript}",
                    transcript.Substring(0, Math.Min(100, transcript.Length))
                );

                // Yield transcription result to frontend BEFORE processing
                yield return new MasterStreamingResponse
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

                // Add transcript as text content for AI
                aiContents.Add(
                    new TextContent($"[Audio file '{fileName}' transcription]: {transcript}")
                );
            }
            else
            {
                // For images, PDFs, documents, etc. - pass directly to AI model natively
                var aiContent = ContentConverter.ConvertToAIContent(content);
                if (aiContent != null)
                {
                    aiContents.Add(aiContent);
                    _logger.LogInformation(
                        "Added {ContentType} content for native multimodal AI processing",
                        content.Type
                    );
                }
                else
                {
                    _logger.LogWarning(
                        "Unable to convert content: Type={Type}, MediaType={MediaType}",
                        content.Type,
                        content.MediaType
                    );
                }
            }
        }

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Processing multi-modal request with {ContentCount} AIContent items for conversation {ConversationId}",
            aiContents.Count,
            conversationId
        );

        // Use native multimodal streaming - AI model handles images, PDFs, documents directly
        await foreach (
            var response in _orchestrator
                .ProcessMultiModalRequestStreamingAsync(
                    aiContents,
                    conversationId,
                    request.EnableThinking
                )
                .WithCancellation(cancellationToken)
        )
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Master agent multi-modal streaming cancelled for {ConnectionId}",
                    Context.ConnectionId
                );
                break;
            }

            yield return new MasterStreamingResponse
            {
                Type = response.Type.ToString(),
                Content = response.Content,
                SubAgentName = response.SubAgentName,
                ToolName = response.ToolName,
                IsComplete = response.Type == ResponseType.Complete,
                Metadata = response.Metadata,
                // Progress step fields
                StepId = response.StepId,
                StepName = response.StepName,
                StepNameAr = response.StepNameAr,
                StepNumber = response.StepNumber,
                TotalSteps = response.TotalSteps,
                StepCompleted = response.Type == ResponseType.StepComplete,
                StepDurationMs = response.StepDurationMs,
                StepDetails = response.StepDetails,
                // Projection result (included in Complete response)
                ProjectionResult = response.ProjectionResult,
            };
        }

        _logger.LogInformation(
            "Master agent multi-modal streaming completed for {ConnectionId}",
            Context.ConnectionId
        );
    }

    /// <summary>
    /// Stream chat responses from the master orchestrator with thread persistence
    /// </summary>
    public async IAsyncEnumerable<MasterStreamingResponse> ChatStreamWithThread(
        string message,
        string? threadIdStr,
        string? conversationId = null,
        bool enableThinking = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        Guid actualThreadId;
        Guid? threadId = null;
        List<ConversationMessage> priorMessages = new();

        // Parse threadId string to Guid if provided
        if (
            !string.IsNullOrEmpty(threadIdStr) && Guid.TryParse(threadIdStr, out var parsedThreadId)
        )
        {
            threadId = parsedThreadId;
        }

        // Create or get thread and load prior messages
        using (var scope = _scopeFactory.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            if (threadId.HasValue && threadId.Value != Guid.Empty)
            {
                actualThreadId = threadId.Value;
                _logger.LogInformation("Using existing thread {ThreadId}", actualThreadId);

                // Load prior messages from database to restore conversation context
                var existingThread = await unitOfWork.Threads.GetByIdWithMessagesAsync(
                    actualThreadId,
                    cancellationToken
                );
                if (existingThread?.Messages != null && existingThread.Messages.Count > 0)
                {
                    priorMessages = existingThread
                        .Messages.OrderBy(m => m.SequenceNumber)
                        .Select(m => new ConversationMessage
                        {
                            Role = m.Role.ToString().ToLower(),
                            Content = m.Content,
                            SubAgentName = m.SubAgentName,
                        })
                        .ToList();

                    _logger.LogInformation(
                        "Loaded {MessageCount} prior messages for thread {ThreadId}",
                        priorMessages.Count,
                        actualThreadId
                    );
                }
            }
            else
            {
                // Create new thread with title from first message
                var thread = await unitOfWork.Threads.CreateAsync(
                    new ChatThread { Title = GenerateThreadTitle(message) },
                    cancellationToken
                );
                actualThreadId = thread.Id;
                _logger.LogInformation("Created new thread {ThreadId}", actualThreadId);
            }

            // Save user message
            await unitOfWork.Messages.AddAsync(
                new Models.ChatMessage
                {
                    ThreadId = actualThreadId,
                    Role = MessageRole.User,
                    Content = message,
                },
                cancellationToken
            );
        }

        var actualConversationId = conversationId ?? actualThreadId.ToString();
        var responseContent = new System.Text.StringBuilder();
        string? subAgentName = null;
        object? projectionResult = null;

        _logger.LogInformation(
            "Master agent streaming chat for {ConnectionId}, Thread: {ThreadId}, Conversation: {ConversationId}, PriorMessages: {PriorCount}",
            Context.ConnectionId,
            actualThreadId,
            actualConversationId,
            priorMessages.Count
        );

        // First, send the thread ID to the client
        yield return new MasterStreamingResponse
        {
            Type = "ThreadCreated",
            Content = actualThreadId.ToString(),
            Metadata = new Dictionary<string, object> { ["threadId"] = actualThreadId.ToString() },
            IsComplete = false,
        };

        // Use the appropriate streaming method based on whether we have prior messages
        var responseStream =
            priorMessages.Count > 0
                ? _orchestrator.ProcessRequestStreamingWithHistoryAsync(
                    message,
                    actualConversationId,
                    priorMessages,
                    enableThinking
                )
                : _orchestrator.ProcessRequestStreamingAsync(
                    message,
                    actualConversationId,
                    enableThinking
                );

        await foreach (var response in responseStream.WithCancellation(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Master agent streaming cancelled for {ConnectionId}",
                    Context.ConnectionId
                );
                break;
            }

            // Collect response content for saving (Content type contains the main response text)
            if (
                !string.IsNullOrEmpty(response.Content)
                && (response.Type == ResponseType.Content || response.Type == ResponseType.Progress)
            )
            {
                responseContent.Append(response.Content);
            }

            if (!string.IsNullOrEmpty(response.SubAgentName))
            {
                subAgentName = response.SubAgentName;
            }

            if (response.ProjectionResult != null)
            {
                projectionResult = response.ProjectionResult;
            }

            yield return new MasterStreamingResponse
            {
                Type = response.Type.ToString(),
                Content = response.Content,
                SubAgentName = response.SubAgentName,
                ToolName = response.ToolName,
                IsComplete = response.Type == ResponseType.Complete,
                Metadata = response.Metadata,
                StepId = response.StepId,
                StepName = response.StepName,
                StepNameAr = response.StepNameAr,
                StepNumber = response.StepNumber,
                TotalSteps = response.TotalSteps,
                StepCompleted = response.Type == ResponseType.StepComplete,
                StepDurationMs = response.StepDurationMs,
                StepDetails = response.StepDetails,
                ProjectionResult = response.ProjectionResult,
            };

            // Save assistant message when complete
            if (response.Type == ResponseType.Complete)
            {
                using var scope = _scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var assistantMessage = new Models.ChatMessage
                {
                    ThreadId = actualThreadId,
                    Role = MessageRole.Assistant,
                    Content = responseContent.ToString(),
                    SubAgentName = subAgentName,
                    MetadataJson =
                        projectionResult != null
                            ? JsonSerializer.Serialize(new { projectionResult })
                            : null,
                };

                await unitOfWork.Messages.AddAsync(assistantMessage, cancellationToken);
                _logger.LogInformation(
                    "Saved assistant response to thread {ThreadId}",
                    actualThreadId
                );
            }
        }

        _logger.LogInformation(
            "Master agent streaming completed for {ConnectionId}",
            Context.ConnectionId
        );
    }

    /// <summary>
    /// Generate a thread title from the first message
    /// </summary>
    private static string GenerateThreadTitle(string content)
    {
        // Take first 50 characters or first sentence, whichever is shorter
        var title = content.Length > 50 ? content[..50] + "..." : content;

        // Clean up the title
        var firstSentenceEnd = title.IndexOfAny(['.', '?', '!', '\n']);
        if (firstSentenceEnd > 0 && firstSentenceEnd < title.Length - 3)
        {
            title = title[..(firstSentenceEnd + 1)];
        }

        return title.Trim();
    }

    /// <summary>
    /// Stream multi-modal chat responses with base64-encoded file data
    /// </summary>
    /// <remarks>
    /// For file uploads with streaming, use this method with base64-encoded data.
    /// The frontend should convert files to base64 before sending via SignalR.
    /// For direct file uploads without streaming, use the HTTP endpoint /chat/multimodal/upload
    /// </remarks>
    /// <param name="request">Multi-modal chat request with base64 data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async IAsyncEnumerable<MasterStreamingResponse> ChatStreamMultiModalWithBase64(
        MultiModalChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // This method uses the same logic as ChatStreamMultiModal
        // Frontend converts files to base64 before sending
        await foreach (var response in ChatStreamMultiModal(request, cancellationToken))
        {
            yield return response;
        }
    }
}
