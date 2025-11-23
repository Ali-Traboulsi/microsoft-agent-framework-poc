using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Helpers;
using AgentFrameworkQuickStart.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentFrameworkQuickStart.Api.Controllers;

[ApiController]
[Route("api/v2/[controller]")]
public class MasterAgentController : ControllerBase
{
    private readonly IMasterOrchestrator _orchestrator;
    private readonly ILogger<MasterAgentController> _logger;
    private readonly AudioTranscriptionService _audioService;
    private readonly AgentThreadManager _threadManager;

    public MasterAgentController(
        IMasterOrchestrator orchestrator,
        ILogger<MasterAgentController> logger,
        AudioTranscriptionService audioService,
        AgentThreadManager threadManager
    )
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _audioService = audioService;
        _threadManager = threadManager;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ApiResponse<OrchestratorResultDto>>> Chat(
        [FromBody] MasterChatRequest request
    )
    {
        try
        {
            _logger.LogInformation("Master agent chat request: {Message}", request.Message);

            var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();
            var result = await _orchestrator.ProcessRequestAsync(
                request.Message,
                conversationId,
                request.EnableThinking
            );

            return Ok(
                new ApiResponse<OrchestratorResultDto>(
                    Success: true,
                    Data: new OrchestratorResultDto
                    {
                        Success = result.Success,
                        Response = result.Response,
                        SubAgentsUsed = result.SubAgentsUsed,
                        DurationMs = result.TotalDurationMs,
                        ErrorMessage = result.ErrorMessage,
                    },
                    Error: null
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in master agent chat");
            return Ok(new ApiResponse<OrchestratorResultDto>(false, null, ex.Message));
        }
    }

    [HttpPost("chat/structured")]
    public async Task<ActionResult<ApiResponse<StructuredOrchestratorResultDto>>> ChatStructured(
        [FromBody] MasterChatRequest request
    )
    {
        try
        {
            _logger.LogInformation(
                "Master agent structured chat request: {Message}",
                request.Message
            );

            var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();
            var result = await _orchestrator.ProcessRequestStructuredAsync(
                request.Message,
                conversationId
            );

            return Ok(
                new ApiResponse<StructuredOrchestratorResultDto>(
                    Success: true,
                    Data: new StructuredOrchestratorResultDto
                    {
                        Success = result.Success,
                        StructuredResponse = result.StructuredResponse,
                        SubAgentsUsed = result.SubAgentsUsed,
                        DurationMs = result.TotalDurationMs,
                        ErrorMessage = result.ErrorMessage,
                    },
                    Error: null
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in master agent structured chat");
            return Ok(new ApiResponse<StructuredOrchestratorResultDto>(false, null, ex.Message));
        }
    }

    /// <summary>
    /// Processes multi-modal chat requests (text, images, audio, files)
    /// </summary>
    /// <remarks>
    /// Accepts mixed content types including:
    /// - Text messages
    /// - Images (base64 encoded or URIs)
    /// - Audio files (base64 encoded or URIs)
    /// - Document files (PDFs, etc.)
    ///
    /// Example request with image:
    /// {
    ///   "message": "What's in this image?",
    ///   "contents": [
    ///     {
    ///       "type": "image",
    ///       "data": "data:image/png;base64,iVBORw0KGgo...",
    ///       "mediaType": "image/png"
    ///     }
    ///   ]
    /// }
    /// </remarks>
    [HttpPost("chat/multimodal")]
    public async Task<ActionResult<ApiResponse<MultiModalChatResponse>>> ChatMultiModal(
        [FromBody] MultiModalChatRequest request
    )
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation(
                "Master agent multi-modal chat request with {ContentCount} content items",
                request.Contents?.Count ?? 0
            );

            // Validate request
            if (request.Contents == null || request.Contents.Count == 0)
            {
                return BadRequest(
                    new ApiResponse<MultiModalChatResponse>(
                        false,
                        null,
                        "Request must include at least one content item"
                    )
                );
            }

            // Validate each content input
            foreach (var content in request.Contents)
            {
                var (isValid, errorMessage) = ContentConverter.ValidateContentInput(content);
                if (!isValid)
                {
                    return BadRequest(
                        new ApiResponse<MultiModalChatResponse>(false, null, errorMessage)
                    );
                }
            }

            // Convert to AIContent
            var aiContents = ContentConverter.ConvertToAIContents(
                request.Contents,
                request.Message ?? string.Empty
            );

            var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();

            // Process through orchestrator
            var result = await _orchestrator.ProcessMultiModalRequestAsync(
                aiContents,
                conversationId,
                request.EnableThinking
            );

            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Get unique content types
            var contentTypes = request.Contents.Select(c => c.Type).Distinct().ToList();

            var response = new MultiModalChatResponse
            {
                Message = result.Response,
                Timestamp = DateTime.UtcNow,
                ProcessingTimeMs = (long)processingTime,
                ContentTypesProcessed = contentTypes,
                ConversationId = conversationId,
            };

            return Ok(new ApiResponse<MultiModalChatResponse>(true, response, null));
        }
        catch (Exception ex)
        {
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Error in master agent multi-modal chat");

            var errorResponse = new MultiModalChatResponse
            {
                Message = $"Error processing request: {ex.Message}",
                Timestamp = DateTime.UtcNow,
                ProcessingTimeMs = (long)processingTime,
                ContentTypesProcessed =
                    request.Contents?.Select(c => c.Type).Distinct().ToList() ?? new List<string>(),
                ConversationId = request.ConversationId ?? Guid.NewGuid().ToString(),
            };

            return Ok(new ApiResponse<MultiModalChatResponse>(false, errorResponse, ex.Message));
        }
    }

    /// <summary>
    /// Processes multi-modal chat requests with file uploads via form-data
    /// </summary>
    /// <remarks>
    /// Upload files directly using multipart/form-data. The API will:
    /// - Read uploaded files (images, audio, PDFs)
    /// - Convert to base64 automatically
    /// - Detect MIME types from file extensions
    /// - Process through vision/audio-capable AI models
    ///
    /// Form fields:
    /// - message: Optional text message
    /// - files: One or more uploaded files
    /// - uris: Optional comma-separated URIs
    /// - conversationId: Optional conversation ID
    ///
    /// Example: Upload image.png + audio.mp3 with message "Analyze these"
    /// </remarks>
    [HttpPost("chat/multimodal/upload")]
    [RequestSizeLimit(100_000_000)] // 100MB limit
    [RequestFormLimits(MultipartBodyLengthLimit = 100_000_000)]
    public async Task<ActionResult<ApiResponse<MultiModalChatResponse>>> ChatMultiModalUpload(
        [FromForm] MultiModalFormRequest request
    )
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation(
                "Master agent multi-modal upload request with {FileCount} files",
                request.Files?.Count ?? 0
            );

            // Validate that at least one file or URI is provided
            var hasFiles = request.Files != null && request.Files.Count > 0;
            var hasUris = !string.IsNullOrEmpty(request.Uris);

            if (!hasFiles && !hasUris)
            {
                return BadRequest(
                    new ApiResponse<MultiModalChatResponse>(
                        false,
                        null,
                        "Request must include at least one file or URI"
                    )
                );
            }

            // Convert form files to AIContent (with audio transcription support)
            var aiContents = await ContentConverter.ConvertFormFilesToAIContents(
                request.Files,
                request.Message,
                request.Uris,
                _audioService
            );

            var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();

            // Process through orchestrator
            var result = await _orchestrator.ProcessMultiModalRequestAsync(
                aiContents,
                conversationId,
                request.EnableThinking
            );

            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Determine content types from files
            var contentTypes = new List<string>();
            if (request.Files != null)
            {
                foreach (var file in request.Files)
                {
                    var mediaType = ContentConverter.DetectMediaType(file.FileName);
                    if (mediaType.StartsWith("image/"))
                        contentTypes.Add("image");
                    else if (mediaType.StartsWith("audio/"))
                        contentTypes.Add("audio");
                    else
                        contentTypes.Add("file");
                }
            }
            if (hasUris)
                contentTypes.Add("uri");

            var response = new MultiModalChatResponse
            {
                Message = result.Response,
                Timestamp = DateTime.UtcNow,
                ProcessingTimeMs = (long)processingTime,
                ContentTypesProcessed = contentTypes.Distinct().ToList(),
                ConversationId = conversationId,
            };

            return Ok(new ApiResponse<MultiModalChatResponse>(true, response, null));
        }
        catch (Exception ex)
        {
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Error in master agent multi-modal upload");

            var errorResponse = new MultiModalChatResponse
            {
                Message = $"Error processing request: {ex.Message}",
                Timestamp = DateTime.UtcNow,
                ProcessingTimeMs = (long)processingTime,
                ContentTypesProcessed = new List<string>(),
                ConversationId = request.ConversationId ?? Guid.NewGuid().ToString(),
            };

            return Ok(new ApiResponse<MultiModalChatResponse>(false, errorResponse, ex.Message));
        }
    }

    [HttpGet("subagents")]
    public ActionResult<ApiResponse<List<SubAgentInfoDto>>> GetSubAgents()
    {
        try
        {
            var subAgents = _orchestrator.GetAvailableSubAgents();
            var dtos = subAgents
                .Select(sa => new SubAgentInfoDto
                {
                    Name = sa.Name,
                    Domain = sa.Domain,
                    Capabilities = sa.Capabilities,
                })
                .ToList();

            return Ok(new ApiResponse<List<SubAgentInfoDto>>(true, dtos, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sub-agents");
            return Ok(new ApiResponse<List<SubAgentInfoDto>>(false, null, ex.Message));
        }
    }

    /// <summary>
    /// Clear conversation history for a specific conversation
    /// </summary>
    [HttpDelete("conversation/{conversationId}")]
    public ActionResult<ApiResponse<bool>> ClearConversation(string conversationId)
    {
        try
        {
            var cleared = _threadManager.ClearThread(conversationId);
            return Ok(
                new ApiResponse<bool>(
                    Success: cleared,
                    Data: cleared,
                    Error: cleared ? null : "Conversation not found"
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing conversation {ConversationId}", conversationId);
            return Ok(new ApiResponse<bool>(false, false, ex.Message));
        }
    }

    /// <summary>
    /// Get statistics about active conversations
    /// </summary>
    [HttpGet("conversation/stats")]
    public ActionResult<ApiResponse<ConversationStatsDto>> GetConversationStats()
    {
        try
        {
            var stats = new ConversationStatsDto
            {
                ActiveConversations = _threadManager.GetActiveThreadCount(),
                ConversationIds = _threadManager.GetActiveConversationIds().ToList(),
            };

            return Ok(
                new ApiResponse<ConversationStatsDto>(Success: true, Data: stats, Error: null)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation stats");
            return Ok(new ApiResponse<ConversationStatsDto>(false, null, ex.Message));
        }
    }
}

public class ConversationStatsDto
{
    public int ActiveConversations { get; set; }
    public List<string> ConversationIds { get; set; } = new();
}

public class MasterChatRequest
{
    public required string Message { get; set; }
    public string? ConversationId { get; set; }
    public bool EnableThinking { get; set; } = false;
}

public class OrchestratorResultDto
{
    public bool Success { get; set; }
    public required string Response { get; set; }
    public List<string> SubAgentsUsed { get; set; } = new();
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SubAgentInfoDto
{
    public required string Name { get; set; }
    public required string Domain { get; set; }
    public required string[] Capabilities { get; set; }
}

public class StructuredOrchestratorResultDto
{
    public bool Success { get; set; }
    public required StructuredAgentResponse StructuredResponse { get; set; }
    public List<string> SubAgentsUsed { get; set; } = new();
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
}
