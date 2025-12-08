using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Middleware;
using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator that coordinates multiple specialized sub-agents.
/// Split into partial classes for maintainability:
/// - MasterOrchestrator.cs (this file) - Core class, fields, constructor, agent creation
/// - MasterOrchestrator.Delegation.cs - Sub-agent delegation methods
/// - MasterOrchestrator.Processing.cs - Request processing methods
/// - MasterOrchestrator.Streaming.cs - Streaming response methods
/// - MasterOrchestrator.MultiModal.cs - Multi-modal request processing
/// - MasterOrchestrator.Helpers.cs - Helper methods and utilities
/// </summary>
public partial class MasterOrchestrator : IMasterOrchestrator
{
    // OpenTelemetry observability
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.MasterOrchestrator",
        "2.0.0"
    );
    private static readonly Meter Meter = new("InvestmentBanking.MasterOrchestrator", "2.0.0");
    private static readonly Counter<long> DelegationCounter = Meter.CreateCounter<long>(
        "orchestrator.delegations",
        description: "Number of sub-agent delegations"
    );
    private static readonly Histogram<double> DelegationDuration = Meter.CreateHistogram<double>(
        "orchestrator.delegation.duration",
        unit: "ms",
        description: "Duration of sub-agent delegations"
    );
    private static readonly Counter<long> DelegationErrorCounter = Meter.CreateCounter<long>(
        "orchestrator.delegation.errors",
        description: "Number of delegation failures"
    );

    private readonly IChatClient _chatClient;
    private readonly IEnumerable<ISubAgent> _subAgents;
    private readonly ILogger<MasterOrchestrator> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Lazy<AIAgent> _masterAgent;
    private readonly Dictionary<string, ISubAgent> _subAgentLookup;
    private readonly WebSearchTools _webSearchTools;
    private readonly StructuredResponseHandler _structuredResponseHandler;
    private readonly AgentThreadManager _threadManager;
    private readonly ProjectionTools _projectionTools;

    public MasterOrchestrator(
        IChatClient chatClient,
        IEnumerable<ISubAgent> subAgents,
        WebSearchTools webSearchTools,
        ILogger<MasterOrchestrator> logger,
        ILoggerFactory loggerFactory,
        StructuredResponseHandler structuredResponseHandler,
        AgentThreadManager threadManager,
        ProjectionTools projectionTools
    )
    {
        _chatClient = chatClient;
        _subAgents = subAgents;
        _webSearchTools = webSearchTools;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _structuredResponseHandler = structuredResponseHandler;
        _threadManager = threadManager;
        _projectionTools = projectionTools;
        _subAgentLookup = subAgents.ToDictionary(sa => sa.Name, sa => sa);
        _masterAgent = new Lazy<AIAgent>(CreateMasterAgentWithMiddleware);
    }

    private AIAgent CreateMasterAgent()
    {
        var instructions = AgentInstructionsLoader.LoadMasterAgentInstructions(_subAgents);

        return _chatClient.CreateAIAgent(
            name: "MasterAgent",
            instructions: instructions,
            tools:
            [
                // Orchestration tools - Master agent delegates to specialized sub-agents
                AIFunctionFactory.Create(DelegateToSubAgent),
                AIFunctionFactory.Create(DelegateToMultipleSubAgents),
                AIFunctionFactory.Create(GetAvailableSubAgentsAsString),
                AIFunctionFactory.Create(_webSearchTools.SearchWeb),
                // Note: SNB Capital and Fund-In operations are handled by ExternalApiServices sub-agent
            ]
        );
    }

    /// <summary>
    /// Create master agent with middleware for event tracking
    /// </summary>
    private AIAgent CreateMasterAgentWithMiddleware()
    {
        var baseAgent = CreateMasterAgent();

        // Wrap agent with delegation event middleware
        return baseAgent
            .AsBuilder()
            .Use(DelegationEventMiddleware.FunctionInvocationMiddleware)
            .Build();
    }
}
