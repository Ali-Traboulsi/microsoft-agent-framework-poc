using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Models;
using AgentFrameworkQuickStart.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentFrameworkQuickStart.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FundsController : ControllerBase
{
    private readonly InvestmentDataStore _dataStore;
    private readonly AgentService _agentService;

    public FundsController(InvestmentDataStore dataStore, AgentService agentService)
    {
        _dataStore = dataStore;
        _agentService = agentService;
    }

    [HttpGet]
    public ActionResult<ApiResponse<IEnumerable<MutualFund>>> GetAllFunds()
    {
        try
        {
            var funds = _dataStore.GetAllMutualFunds();
            return Ok(new ApiResponse<IEnumerable<MutualFund>>(true, funds, null));
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new ApiResponse<IEnumerable<MutualFund>>(false, null, ex.Message)
            );
        }
    }

    [HttpGet("{fundId}")]
    public ActionResult<ApiResponse<MutualFund>> GetFund(string fundId)
    {
        try
        {
            var fund = _dataStore.GetMutualFund(fundId) ?? _dataStore.GetMutualFundBySymbol(fundId);
            if (fund == null)
                return NotFound(
                    new ApiResponse<MutualFund>(false, null, $"Fund {fundId} not found")
                );

            return Ok(new ApiResponse<MutualFund>(true, fund, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<MutualFund>(false, null, ex.Message));
        }
    }

    [HttpPost("search")]
    public async Task<ActionResult<ApiResponse<string>>> SearchFunds(
        [FromBody] FundSearchRequest request
    )
    {
        try
        {
            var agent = _agentService.GetAdvisorAgent();
            var query = "Find mutual funds";

            if (!string.IsNullOrEmpty(request.Category))
                query += $" in {request.Category} category";

            if (!string.IsNullOrEmpty(request.RiskLevel))
                query += $" with {request.RiskLevel} risk";

            if (request.MinReturn.HasValue)
                query += $" with at least {request.MinReturn}% return";

            var result = await agent.RunAsync(query);
            return Ok(new ApiResponse<string>(true, result.Text, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<string>(false, null, ex.Message));
        }
    }

    [HttpPost("compare")]
    public async Task<ActionResult<ApiResponse<string>>> CompareFunds(
        [FromBody] string[] fundSymbols
    )
    {
        try
        {
            var agent = _agentService.GetAdvisorAgent();
            var symbols = string.Join(", ", fundSymbols);
            var result = await agent.RunAsync($"Compare these funds: {symbols}");
            return Ok(new ApiResponse<string>(true, result.Text, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<string>(false, null, ex.Message));
        }
    }

    [HttpGet("{fundId}/details")]
    public async Task<ActionResult<ApiResponse<string>>> GetFundDetails(string fundId)
    {
        try
        {
            var agent = _agentService.GetAdvisorAgent();
            var result = await agent.RunAsync($"Show detailed information about fund {fundId}");
            return Ok(new ApiResponse<string>(true, result.Text, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<string>(false, null, ex.Message));
        }
    }
}
