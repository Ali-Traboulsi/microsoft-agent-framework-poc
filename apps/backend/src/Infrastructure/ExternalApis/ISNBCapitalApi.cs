using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Infrastructure.ExternalApis;

/// <summary>
/// Interface for SNB Capital API operations - uses existing DTOs from Services
/// </summary>
public interface ISNBCapitalApi
{
    Task<MutualFundsListResponse> GetMutualFundsAsync(string cif);
    Task<CustomerCompletePortfolioData> GetCustomerDataAsync(string cif);
}
