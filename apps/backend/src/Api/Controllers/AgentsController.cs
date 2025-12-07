using AgentFrameworkQuickStart.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgentFrameworkQuickStart.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly AgentService _agentService;

    public AgentsController(AgentService agentService)
    {
        _agentService = agentService;
    }

    [HttpGet]
    public ActionResult<ApiResponse<string[]>> GetAvailableAgents()
    {
        var agents = new[]
        {
            "PortfolioManager",
            "InvestmentAdvisor",
            "AccountServices",
            "ComplianceOfficer",
        };
        return Ok(new ApiResponse<string[]>(true, agents, null));
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ApiResponse<ChatResponse>>> Chat([FromBody] ChatRequest request)
    {
        try
        {
            var agent = _agentService.GetAgentByName(request.AgentName);
            var result = await agent.RunAsync(request.Message);

            var response = new ChatResponse(
                result.Text ?? "No response",
                request.AgentName,
                DateTime.UtcNow
            );

            return Ok(new ApiResponse<ChatResponse>(true, response, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<ChatResponse>(false, null, ex.Message));
        }
    }
}
