using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Infrastructure.ExternalApis;

/// <summary>
/// Adapter that implements clean interface using existing SNBCapitalApiService
/// </summary>
public sealed class SNBCapitalApiAdapter : ISNBCapitalApi
{
    private readonly SNBCapitalApiService _service;
    private readonly ILogger<SNBCapitalApiAdapter> _logger;

    public SNBCapitalApiAdapter(SNBCapitalApiService service, ILogger<SNBCapitalApiAdapter> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<MutualFundsListResponse> GetMutualFundsAsync(string cif)
    {
        try
        {
            return await _service.GetMutualFundsAsync(cif);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch mutual funds for CIF {Cif}", cif);
            return new MutualFundsListResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<CustomerCompletePortfolioData> GetCustomerDataAsync(string cif)
    {
        try
        {
            return await _service.GetCompleteCustomerDataAsync(cif);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch customer data for CIF {Cif}", cif);
            return new CustomerCompletePortfolioData { Cif = cif };
        }
    }
}
