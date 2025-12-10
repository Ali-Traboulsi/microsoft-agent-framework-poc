using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Middleware;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;
using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator that coordinates multiple specialized sub-agents.
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
    private readonly AgentThreadManager _threadManager;
    private readonly SubAgentThreadManager _subAgentThreadManager;
    private readonly ProjectionTools _projectionTools;

    // Intelligence Layer (P0)
    private readonly IIntentClassifier _intentClassifier;
    private readonly IConversationContextStore _contextStore;

    // Metrics for intelligence layer
    private static readonly Counter<long> IntentClassificationCounter = Meter.CreateCounter<long>(
        "orchestrator.intent_classifications",
        description: "Number of intent classifications performed"
    );
    private static readonly Histogram<double> IntentClassificationDuration =
        Meter.CreateHistogram<double>(
            "orchestrator.intent_classification.duration",
            unit: "ms",
            description: "Duration of intent classification"
        );

    public MasterOrchestrator(
        IChatClient chatClient,
        IEnumerable<ISubAgent> subAgents,
        WebSearchTools webSearchTools,
        ILogger<MasterOrchestrator> logger,
        ILoggerFactory loggerFactory,
        AgentThreadManager threadManager,
        SubAgentThreadManager subAgentThreadManager,
        ProjectionTools projectionTools,
        IIntentClassifier intentClassifier,
        IConversationContextStore contextStore
    )
    {
        _chatClient = chatClient;
        _subAgents = subAgents;
        _webSearchTools = webSearchTools;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _threadManager = threadManager;
        _subAgentThreadManager = subAgentThreadManager;
        _projectionTools = projectionTools;
        _intentClassifier = intentClassifier;
        _contextStore = contextStore;
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
