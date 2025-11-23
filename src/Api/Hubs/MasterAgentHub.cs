using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Helpers;
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

    public MasterAgentHub(
        IMasterOrchestrator orchestrator,
        ILogger<MasterAgentHub> logger,
        AudioTranscriptionService audioService
    )
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _audioService = audioService;
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
            };
        }

        _logger.LogInformation(
            "Master agent multi-modal streaming completed for {ConnectionId}",
            Context.ConnectionId
        );
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

/// <summary>
/// Streaming response from master agent
/// </summary>
public class MasterStreamingResponse
{
    public required string Type { get; set; }
    public string? Content { get; set; }
    public string? SubAgentName { get; set; }
    public string? ToolName { get; set; }
    public bool IsComplete { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
