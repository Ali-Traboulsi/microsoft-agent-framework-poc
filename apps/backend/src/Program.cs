using AgentFrameworkQuickStart.Api.Hubs;
using AgentFrameworkQuickStart.Api.Middleware;
using AgentFrameworkQuickStart.Infrastructure.DependencyInjection;
using AgentFrameworkQuickStart.Infrastructure.Persistence;
using AgentFrameworkQuickStart.Services;

var builder = WebApplication.CreateBuilder(args);

// Get OpenAI configuration from environment or configuration
var openAiApiKey =
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException(
        "OpenAI API key not found. Set OPENAI_API_KEY environment variable or configure OpenAI:ApiKey."
    );

Console.WriteLine($"🔧 Using OpenAI with model: gpt-4o (with vision support)");

// Add services to container
builder.Services.AddControllers();

// ===== Register Services using DI Extension Methods =====

// Add SignalR with custom configuration
builder.Services.AddSignalRServices();

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(
        "v2",
        new() { Title = "Banking Investment Agent API - Master Orchestrator", Version = "v2" }
    );
});

// ===== Database & Persistence =====
builder.Services.AddPersistence(builder.Configuration);

// ===== Core Services =====
builder.Services.AddCoreServices();

// ===== Tools =====
builder.Services.AddTools();

// ===== External APIs & HTTP Clients =====
builder.Services.AddExternalApis();

// ===== SignalR Hub Handlers =====
builder.Services.AddSignalRHandlers();

// ===== Workflows =====
builder.Services.AddWorkflows();

// ===== AI & Orchestration =====
builder.Services.AddAIChatClient(openAiApiKey);
builder.Services.AddSubAgents();
builder.Services.AddOrchestration();

// ===== Intelligence Layer (P0 + P1) =====
builder.Services.AddIntelligenceLayer();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // Get allowed origins from environment or use defaults
        var allowedOrigins =
            Environment
                .GetEnvironmentVariable("ALLOWED_ORIGINS")
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? ["http://localhost:3000", "http://localhost:5173"];

        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var app = builder.Build();

// ===== Database Migration =====
// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        dbContext.Database.EnsureCreated();
        Console.WriteLine("✅ Database initialized successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
        throw;
    }
}

// Initialize data store with seed data
var dataStore = app.Services.GetRequiredService<InvestmentDataStore>();
dataStore.SeedData();

// Initialize delegation event notifier for real-time SignalR push
var delegationNotifier = app.Services.GetRequiredService<IDelegationEventNotifier>();
DelegationEventNotifierAccessor.SetInstance(delegationNotifier);
Console.WriteLine("✅ Delegation event notifier initialized for real-time SignalR push");

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

// Health check endpoint for Docker/Kubernetes
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// SignalR Hubs
app.MapHub<AgentHub>("/hubs/agent"); // Legacy individual agents
app.MapHub<MasterAgentHub>("/hubs/master"); // New master orchestrator
app.MapHub<CoordinationHub>("/hubs/coordination"); // Sub-agent coordination protocol

// Root endpoint
app.MapGet(
    "/",
    () =>
        new
        {
            Name = "Banking Investment Agent API - Multi-Agent System",
            Version = "2.0.0",
            Architecture = "Master Orchestrator with Specialized Sub-Agents",
            Endpoints = new
            {
                // V2 - Master Orchestrator
                MasterAgent = "/api/v2/masteragent",
                MasterAgentHub = "/hubs/master",

                // Profit Projection Workflow
                ProfitProjection = "/api/projection",

                // V1 - Legacy Individual Agents
                Accounts = "/api/accounts",
                Portfolios = "/api/portfolios",
                Funds = "/api/funds",
                Agents = "/api/agents",
                AgentHub = "/hubs/agent",

                // Documentation
                Swagger = "/swagger",
            },
            SubAgents = new[]
            {
                "PortfolioManager - Portfolio management and analysis",
                "InvestmentAdvisor - Fund recommendations and research",
                "AccountServices - Account operations and management",
                "ComplianceOfficer - Regulatory compliance and risk assessment",
            },
            Workflows = new[]
            {
                "ProfitProjection - Calculate estimated returns with multiple scenarios",
            },
        }
);

Console.WriteLine("\n=== Banking Investment Agent API - Multi-Agent System ===");
Console.WriteLine("API is running on: http://localhost:5000");
Console.WriteLine("Swagger UI: http://localhost:5000/swagger");
Console.WriteLine("\nV2 Endpoints (Master Orchestrator):");
Console.WriteLine("  - REST API: http://localhost:5000/api/v2/masteragent");
Console.WriteLine("  - SignalR: http://localhost:5000/hubs/master");
Console.WriteLine("\nProfit Projection Workflow:");
Console.WriteLine("  - POST: http://localhost:5000/api/projection/calculate");
Console.WriteLine("  - GET:  http://localhost:5000/api/projection/quick-projection");
Console.WriteLine("\nV1 Endpoints (Legacy Individual Agents):");
Console.WriteLine("  - SignalR: http://localhost:5000/hubs/agent");
Console.WriteLine("=========================================================\n");

app.Run();
