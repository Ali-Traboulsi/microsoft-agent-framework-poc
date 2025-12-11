using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Infrastructure.ExternalApis;
using AgentFrameworkQuickStart.Infrastructure.Persistence;
using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Services.FundIn;
using Microsoft.EntityFrameworkCore;

namespace AgentFrameworkQuickStart.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering infrastructure services.
/// </summary>
public static class InfrastructureExtensions
{
    /// <summary>
    /// Adds database context and persistence services.
    /// </summary>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Configure SQLite database for chat thread persistence
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "agentchat.db");
        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

        // Unit of Work and Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        Console.WriteLine($"📦 Database configured: {dbPath}");

        return services;
    }

    /// <summary>
    /// Adds external API services and HTTP clients.
    /// </summary>
    public static IServiceCollection AddExternalApis(this IServiceCollection services)
    {
        // HTTP client for web search
        services.AddHttpClient();

        // SNB Capital API Service (with extended timeout for cold starts)
        services.AddHttpClient<SNBCapitalApiService>(client =>
        {
            client.BaseAddress = new Uri("https://snbc-api.onrender.com/snbc/api/v1/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(120); // 2 minutes for cold starts
        });

        // Fund-In Service
        services.AddHttpClient<FundInService>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(120); // 2 minutes for cold starts
        });

        // SNB Capital API Adapter (Clean Architecture abstraction)
        services.AddScoped<ISNBCapitalApi, SNBCapitalApiAdapter>();

        // Test Token Service (for demo JWT token generation)
        services.AddSingleton<TestTokenService>();

        return services;
    }
}
