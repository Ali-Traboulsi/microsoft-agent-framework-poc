using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Models;
using AgentFrameworkQuickStart.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentFrameworkQuickStart.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfoliosController : ControllerBase
{
    private readonly InvestmentDataStore _dataStore;
    private readonly AgentService _agentService;

    public PortfoliosController(InvestmentDataStore dataStore, AgentService agentService)
    {
        _dataStore = dataStore;
        _agentService = agentService;
    }

    [HttpGet]
    public ActionResult<ApiResponse<IEnumerable<Portfolio>>> GetAllPortfolios(
        [FromQuery] string? accountId = null
    )
    {
        try
        {
            var portfolios =
                accountId != null
                    ? _dataStore.GetPortfoliosByAccount(accountId)
                    : _dataStore.GetAllPortfolios();

            return Ok(new ApiResponse<IEnumerable<Portfolio>>(true, portfolios, null));
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new ApiResponse<IEnumerable<Portfolio>>(false, null, ex.Message)
            );
        }
    }

    [HttpGet("{portfolioId}")]
    public ActionResult<ApiResponse<Portfolio>> GetPortfolio(string portfolioId)
    {
        try
        {
            var portfolio = _dataStore.GetPortfolio(portfolioId);
            if (portfolio == null)
                return NotFound(
                    new ApiResponse<Portfolio>(false, null, $"Portfolio {portfolioId} not found")
                );

            return Ok(new ApiResponse<Portfolio>(true, portfolio, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<Portfolio>(false, null, ex.Message));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<string>>> CreatePortfolio(
        [FromBody] PortfolioRequest request
    )
    {
        try
        {
            var agent = _agentService.GetPortfolioAgent();
            var result = await agent.RunAsync(
                $"Create portfolio '{request.PortfolioName}' for account {request.AccountId} with {request.Strategy} strategy"
            );
            return Ok(new ApiResponse<string>(true, result.Text, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<string>(false, null, ex.Message));
        }
    }

    [HttpGet("{portfolioId}/details")]
    public async Task<ActionResult<ApiResponse<string>>> GetPortfolioDetails(string portfolioId)
    {
        try
        {
            var agent = _agentService.GetPortfolioAgent();
            var result = await agent.RunAsync($"Show complete details for portfolio {portfolioId}");
            return Ok(new ApiResponse<string>(true, result.Text, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<string>(false, null, ex.Message));
        }
    }

    [HttpGet("{portfolioId}/allocation")]
    public async Task<ActionResult<ApiResponse<string>>> GetAllocation(string portfolioId)
    {
        try
        {
            var agent = _agentService.GetPortfolioAgent();
            var result = await agent.RunAsync(
                $"Show allocation breakdown for portfolio {portfolioId}"
            );
            return Ok(new ApiResponse<string>(true, result.Text, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<string>(false, null, ex.Message));
        }
    }

    [HttpPost("{portfolioId}/invest")]
    public async Task<ActionResult<ApiResponse<string>>> InvestInFund(
        string portfolioId,
        [FromBody] InvestmentRequest request
    )
    {
        try
        {
            var agent = _agentService.GetPortfolioAgent();
            var result = await agent.RunAsync(
                $"Invest ${request.Amount} from account {request.AccountId} into portfolio {portfolioId} by purchasing {request.FundSymbol}"
            );
            return Ok(new ApiResponse<string>(true, result.Text, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<string>(false, null, ex.Message));
        }
    }
}
