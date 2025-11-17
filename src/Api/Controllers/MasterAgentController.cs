using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgentFrameworkQuickStart.Api.Controllers;

[ApiController]
[Route("api/v2/[controller]")]
public class MasterAgentController : ControllerBase
{
    private readonly IMasterOrchestrator _orchestrator;
    private readonly ILogger<MasterAgentController> _logger;

    public MasterAgentController(
        IMasterOrchestrator orchestrator,
        ILogger<MasterAgentController> logger
    )
    {
        _orchestrator = orchestrator;
        _logger = logger;
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
            var result = await _orchestrator.ProcessRequestAsync(request.Message, conversationId);

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
}

public class MasterChatRequest
{
    public required string Message { get; set; }
    public string? ConversationId { get; set; }
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
