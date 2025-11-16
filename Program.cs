using AgentFrameworkQuickStart.Api;
using AgentFrameworkQuickStart.Api.Hubs;
using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Tools;

var builder = WebApplication.CreateBuilder(args);

// Get OpenAI API key from environment or configuration
var openAiApiKey =
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException(
        "OpenAI API key not found. Set OPENAI_API_KEY environment variable."
    );

// Add services to container
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Banking Investment Agent API", Version = "v1" });
});

// Register application services
builder.Services.AddSingleton<InvestmentDataStore>();
builder.Services.AddScoped<AccountTools>();
builder.Services.AddScoped<PortfolioTools>();
builder.Services.AddScoped<MutualFundTools>();
builder.Services.AddScoped(sp =>
{
    var dataStore = sp.GetRequiredService<InvestmentDataStore>();
    var accountTools = sp.GetRequiredService<AccountTools>();
    var portfolioTools = sp.GetRequiredService<PortfolioTools>();
    var fundTools = sp.GetRequiredService<MutualFundTools>();
    return new AgentService(dataStore, accountTools, portfolioTools, fundTools, openAiApiKey);
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins("http://localhost:3000", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Initialize data store with seed data
var dataStore = app.Services.GetRequiredService<InvestmentDataStore>();
dataStore.SeedData();

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AgentHub>("/hubs/agent");

// Root endpoint
app.MapGet(
    "/",
    () =>
        new
        {
            Name = "Banking Investment Agent API",
            Version = "1.0.0",
            Endpoints = new
            {
                Accounts = "/api/accounts",
                Portfolios = "/api/portfolios",
                Funds = "/api/funds",
                Agents = "/api/agents",
                AgentHub = "/hubs/agent",
                Swagger = "/swagger",
            },
        }
);

Console.WriteLine("\n=== Banking Investment Agent API ===");
Console.WriteLine("API is running on: http://localhost:5000");
Console.WriteLine("Swagger UI: http://localhost:5000/swagger");
Console.WriteLine("SignalR Hub: http://localhost:5000/hubs/agent");
Console.WriteLine("=====================================\n");

app.Run();
