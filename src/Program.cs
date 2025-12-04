using AgentFrameworkQuickStart.Api;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Hubs;
using AgentFrameworkQuickStart.Api.Orchestration;
using AgentFrameworkQuickStart.Api.SubAgents;
using AgentFrameworkQuickStart.Api.Workflows;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Executors;
using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Tools;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.AI;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
builder.Services.AddSignalR(options =>
{
    // Increase message size limit for multimodal content (base64-encoded files)
    // Default is 32KB, increase to 100MB to support large audio/image files
    options.MaximumReceiveMessageSize = 100 * 1024 * 1024; // 100MB
});

// Add HttpClient for web search
builder.Services.AddHttpClient();

// Configure OpenTelemetry for comprehensive observability
var serviceName = "InvestmentBankingAgents";
var serviceVersion = "2.0.0";
var enableAzureMonitor = !string.IsNullOrEmpty(
    builder.Configuration["ApplicationInsights:ConnectionString"]
);

// Control console output verbosity (set to false to reduce noise)
var enableConsoleExporter = builder.Configuration.GetValue("OpenTelemetry:ConsoleExporter", true);
var enableRuntimeMetrics = builder.Configuration.GetValue("OpenTelemetry:RuntimeMetrics", false); // Disabled by default - too verbose

builder
    .Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
        resource
            .AddService(
                serviceName: serviceName,
                serviceVersion: serviceVersion,
                serviceInstanceId: Environment.MachineName
            )
            .AddAttributes(
                new Dictionary<string, object>
                {
                    ["deployment.environment"] =
                        builder.Environment.EnvironmentName ?? "Development",
                    ["service.namespace"] = "AgentFramework",
                }
            )
    )
    .WithTracing(tracing =>
    {
        tracing
            // Built-in Microsoft Agent Framework tracing
            .AddSource("Microsoft.Agents.AI")
            .AddSource("Microsoft.Extensions.AI")
            // Custom tracing for our orchestration
            .AddSource("InvestmentBanking.MasterOrchestrator")
            .AddSource("InvestmentBanking.SubAgents")
            .AddSource("InvestmentBanking.Workflows")
            // Profit Projection Workflow tracing
            .AddSource("InvestmentBanking.ProfitProjection.Workflow")
            .AddSource("InvestmentBanking.ProfitProjection.CustomerContext")
            .AddSource("InvestmentBanking.ProfitProjection.HistoricalAnalyzer")
            .AddSource("InvestmentBanking.ProfitProjection.MarketConditions")
            .AddSource("InvestmentBanking.ProfitProjection.FundSelection")
            .AddSource("InvestmentBanking.ProfitProjection.Aggregator")
            .AddSource("InvestmentBanking.ProfitProjection.ScenarioBuilder")
            .AddSource("InvestmentBanking.ProfitProjection.Recommendations")
            // ASP.NET Core automatic instrumentation
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequest = (activity, httpRequest) =>
                {
                    activity.SetTag("http.request_id", httpRequest.HttpContext.TraceIdentifier);
                };
                options.EnrichWithHttpResponse = (activity, httpResponse) =>
                {
                    activity.SetTag("http.response.status_code", httpResponse.StatusCode);
                };
            })
            // HTTP client instrumentation for outbound calls
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequestMessage = (activity, httpRequest) =>
                {
                    activity.SetTag("http.request.method", httpRequest.Method.ToString());
                    activity.SetTag("http.request.uri", httpRequest.RequestUri?.ToString());
                };
            })
            // Console exporter for development
            .AddConsoleExporter(options =>
            {
                options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
            });

        // Azure Monitor uses Azure SDK, not OTLP
    })
    .WithMetrics(metrics =>
    {
        metrics
            // Built-in Microsoft Agent Framework metrics
            .AddMeter("Microsoft.Agents.AI")
            .AddMeter("Microsoft.Extensions.AI")
            // Custom metrics for our orchestration
            .AddMeter("InvestmentBanking.MasterOrchestrator")
            .AddMeter("InvestmentBanking.SubAgents")
            .AddMeter("InvestmentBanking.Workflows")
            // Profit Projection Workflow metrics
            .AddMeter("InvestmentBanking.ProfitProjection.Workflow")
            .AddMeter("InvestmentBanking.ProfitProjection.CustomerContext")
            .AddMeter("InvestmentBanking.ProfitProjection.HistoricalAnalyzer")
            .AddMeter("InvestmentBanking.ProfitProjection.MarketConditions")
            .AddMeter("InvestmentBanking.ProfitProjection.FundSelection")
            .AddMeter("InvestmentBanking.ProfitProjection.Aggregator")
            .AddMeter("InvestmentBanking.ProfitProjection.ScenarioBuilder")
            .AddMeter("InvestmentBanking.ProfitProjection.Recommendations")
            // ASP.NET Core runtime metrics
            .AddAspNetCoreInstrumentation()
            // HTTP client metrics
            .AddHttpClientInstrumentation();

        // .NET Runtime metrics (verbose - enable only if needed)
        if (enableRuntimeMetrics)
        {
            metrics.AddRuntimeInstrumentation();
        }

        // Console exporter for development
        if (enableConsoleExporter)
        {
            metrics.AddConsoleExporter();
        }

        // Azure Monitor uses Azure SDK, not OTLP
    });

// Add Azure Monitor if connection string is configured
if (enableAzureMonitor)
{
    builder
        .Services.AddOpenTelemetry()
        .UseAzureMonitor(options =>
        {
            options.ConnectionString = builder.Configuration[
                "ApplicationInsights:ConnectionString"
            ];
        });

    Console.WriteLine(
        $"✅ Azure Monitor enabled for '{serviceName}' v{serviceVersion} in {builder.Environment.EnvironmentName} environment"
    );
}
else
{
    Console.WriteLine($"ℹ️  OpenTelemetry configured for '{serviceName}' v{serviceVersion}");
    Console.WriteLine(
        $"   Console Exporter: {(enableConsoleExporter ? "Enabled (verbose)" : "Disabled")}"
    );
    Console.WriteLine(
        $"   Runtime Metrics: {(enableRuntimeMetrics ? "Enabled" : "Disabled (too verbose)")}"
    );
    Console.WriteLine(
        "   To enable Azure Monitor, set ApplicationInsights:ConnectionString in configuration"
    );
    Console.WriteLine("   To disable console output, set OpenTelemetry:ConsoleExporter=false");
}

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(
        "v1",
        new() { Title = "Banking Investment Agent API - Multi-Agent System", Version = "v1" }
    );
    c.SwaggerDoc(
        "v2",
        new() { Title = "Banking Investment Agent API - Master Orchestrator", Version = "v2" }
    );
});

// Register data store (Singleton - shared across all requests)
builder.Services.AddSingleton<InvestmentDataStore>();

// Register agent thread manager (Singleton - manages conversation threads)
builder.Services.AddSingleton<AgentThreadManager>();

// Register audio transcription service (Scoped)
builder.Services.AddHttpClient<AudioTranscriptionService>();

// Register tools (Scoped - one instance per request)
builder.Services.AddScoped<AccountTools>();
builder.Services.AddScoped<PortfolioTools>();
builder.Services.AddScoped<MutualFundTools>();
builder.Services.AddScoped<WebSearchTools>();
builder.Services.AddScoped<ProjectionTools>();

// Register SNB Capital API Service (Singleton - shared HttpClient)
builder.Services.AddHttpClient<SNBCapitalApiService>(client =>
{
    client.BaseAddress = new Uri("https://snbc-api.onrender.com/snbc/api/v1/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Register Profit Projection Workflow Executors (Scoped)
builder.Services.AddScoped<CustomerContextExecutor>();
builder.Services.AddScoped<HistoricalAnalyzer>();
builder.Services.AddScoped<MarketConditionsAnalyzer>();
builder.Services.AddScoped<FundSelectionAnalyzer>();
builder.Services.AddScoped<ResultsAggregator>();
builder.Services.AddScoped<ScenarioBuilder>();
builder.Services.AddScoped<RecommendationEngine>();

// Register Profit Projection Workflow (Scoped)
builder.Services.AddScoped<ProfitProjectionWorkflow>();

// Register IChatClient using OpenAI (Scoped - one instance per request)
builder.Services.AddScoped<IChatClient>(sp =>
{
    var chatClient = new OpenAI.Chat.ChatClient(model: "gpt-4o", apiKey: openAiApiKey);
    return chatClient.AsIChatClient();
});

// Register Sub-Agents (Scoped - one instance per request)
builder.Services.AddScoped<ISubAgent, PortfolioManagerSubAgent>();
builder.Services.AddScoped<ISubAgent, InvestmentAdvisorSubAgent>();
builder.Services.AddScoped<ISubAgent, AccountServicesSubAgent>();
builder.Services.AddScoped<ISubAgent, ComplianceOfficerSubAgent>();

// Register helper classes for orchestration
builder.Services.AddScoped<StructuredResponseHandler>();

// Register Master Orchestrator (Scoped - one instance per request)
builder.Services.AddScoped<IMasterOrchestrator, MasterOrchestrator>();

// Register Workflows (Scoped - one instance per request)
builder.Services.AddScoped<IWorkflow, CompleteInvestmentWorkflow>();

// Legacy AgentService (for backward compatibility with existing endpoints)
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

// SignalR Hubs
app.MapHub<AgentHub>("/hubs/agent"); // Legacy individual agents
app.MapHub<MasterAgentHub>("/hubs/master"); // New master orchestrator

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
