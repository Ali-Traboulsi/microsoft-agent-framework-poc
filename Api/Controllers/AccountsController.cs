using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Models;
using AgentFrameworkQuickStart.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentFrameworkQuickStart.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly InvestmentDataStore _dataStore;
    private readonly AgentService _agentService;

    public AccountsController(InvestmentDataStore dataStore, AgentService agentService)
    {
        _dataStore = dataStore;
        _agentService = agentService;
    }

    [HttpGet]
    public ActionResult<ApiResponse<IEnumerable<Account>>> GetAllAccounts()
    {
        try
        {
            var accounts = _dataStore.GetAllAccounts();
            return Ok(new ApiResponse<IEnumerable<Account>>(true, accounts, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<IEnumerable<Account>>(false, null, ex.Message));
        }
    }

    [HttpGet("{accountId}")]
    public ActionResult<ApiResponse<Account>> GetAccount(string accountId)
    {
        try
        {
            var account = _dataStore.GetAccount(accountId);
            if (account == null)
                return NotFound(
                    new ApiResponse<Account>(false, null, $"Account {accountId} not found")
                );

            return Ok(new ApiResponse<Account>(true, account, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<Account>(false, null, ex.Message));
        }
    }

    [HttpGet("{accountId}/balance")]
    public async Task<ActionResult<ApiResponse<string>>> GetAccountBalance(string accountId)
    {
        try
        {
            var agent = _agentService.GetAccountAgent();
            var result = await agent.RunAsync($"Show balance for account {accountId}");
            return Ok(new ApiResponse<string>(true, result.Text, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<string>(false, null, ex.Message));
        }
    }

    [HttpPost("{accountId}/deposit")]
    public async Task<ActionResult<ApiResponse<string>>> DepositFunds(
        string accountId,
        [FromBody] decimal amount
    )
    {
        try
        {
            var agent = _agentService.GetAccountAgent();
            var result = await agent.RunAsync($"Deposit ${amount} into account {accountId}");
            return Ok(new ApiResponse<string>(true, result.Text, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<string>(false, null, ex.Message));
        }
    }

    [HttpGet("{accountId}/transactions")]
    public ActionResult<ApiResponse<IEnumerable<Transaction>>> GetTransactions(
        string accountId,
        [FromQuery] int limit = 10
    )
    {
        try
        {
            var transactions = _dataStore.GetTransactionsByAccount(accountId).Take(limit);
            return Ok(new ApiResponse<IEnumerable<Transaction>>(true, transactions, null));
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new ApiResponse<IEnumerable<Transaction>>(false, null, ex.Message)
            );
        }
    }
}
