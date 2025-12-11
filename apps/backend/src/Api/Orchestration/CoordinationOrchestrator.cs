using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Core.Domain.Coordination;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Orchestrates sub-agent coordination with real-time streaming events.
/// Creates execution plans, manages shared context, resolves conflicts,
/// and provides a unified synthesis of all agent findings.
/// </summary>
public partial class CoordinationOrchestrator(
    IEnumerable<ISubAgent> subAgents,
    IChatClient chatClient,
    ILogger<CoordinationOrchestrator> logger
)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.Coordination.Orchestrator",
        "2.0.0"
    );

    private static readonly Meter Meter = new(
        "InvestmentBanking.Coordination.Orchestrator",
        "2.0.0"
    );

    private static readonly Counter<long> CoordinationCounter = Meter.CreateCounter<long>(
        "coordination.executions",
        description: "Number of coordinations"
    );

    private static readonly Histogram<double> CoordinationDuration = Meter.CreateHistogram<double>(
        "coordination.duration",
        unit: "ms"
    );

    private static readonly Counter<long> AgentInvocations = Meter.CreateCounter<long>(
        "coordination.agent_invocations",
        description: "Agent invocations"
    );

    private static readonly Counter<long> ConflictCounter = Meter.CreateCounter<long>(
        "coordination.conflicts",
        description: "Conflicts detected"
    );

    private readonly Dictionary<string, ISubAgent> _agentsByName = subAgents.ToDictionary(
        a => a.Name,
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>
    /// Execute coordinated request with streaming events
    /// </summary>
    public async IAsyncEnumerable<CoordinationEvent> ExecuteCoordinatedAsync(
        string request,
        string conversationId,
        UserIntent intent,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "Coordination.Execute",
            ActivityKind.Internal
        );
        activity?.SetTag("conversation_id", conversationId);
        activity?.SetTag("intent.type", intent.PrimaryIntent.ToString());

        var sw = Stopwatch.StartNew();
        var context = new CoordinationContext { ConversationId = conversationId };

        logger.LogInformation(
            "Starting coordination for conversation {ConversationId} with {AgentCount} available agents",
            conversationId,
            _agentsByName.Count
        );

        // Phase 1: Create Plan
        var plan = await BuildExecutionPlanAsync(request, intent, context, cancellationToken);
        context.CurrentPlan = plan;
        yield return CoordinationEventFactory.PlanCreated(plan);

        logger.LogInformation("Execution plan created with {StepCount} steps", plan.Steps.Count);

        // Phase 2: Execute steps (respecting dependencies)
        while (!plan.IsComplete)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var executableSteps = plan.GetExecutableSteps().ToList();

            if (executableSteps.Count == 0)
            {
                logger.LogWarning("No executable steps but plan not complete. Breaking.");
                break;
            }

            // Execute steps sequentially for now (parallel in future)
            foreach (var step in executableSteps)
            {
                var events = await ExecuteStepWithEventsAsync(
                    step,
                    context,
                    plan.Steps.Count,
                    cancellationToken
                );
                foreach (var evt in events)
                {
                    yield return evt;
                }
            }
        }

        // Phase 3: Conflict Resolution
        var conflicts = context.GetAllConflicts();
        if (conflicts.Count > 0)
        {
            yield return new CoordinationEvent
            {
                Type = CoordinationEventType.ConflictDetected,
                Content = $"Detected {conflicts.Count} conflict(s) between agents",
            };

            var resolutionEvents = await ResolveConflictsWithEventsAsync(
                conflicts,
                context,
                cancellationToken
            );
            foreach (var evt in resolutionEvents)
            {
                yield return evt;
            }
        }

        // Phase 4: Synthesize Response
        yield return CoordinationEventFactory.SynthesisStarted();

        var synthesis = await SynthesizeResponseAsync(context, request, cancellationToken);

        sw.Stop();

        // Record metrics
        CoordinationCounter.Add(1, new KeyValuePair<string, object?>("success", true));
        CoordinationDuration.Record(sw.ElapsedMilliseconds);

        activity?.SetStatus(ActivityStatusCode.Ok);

        yield return CoordinationEventFactory.SynthesisCompleted(
            synthesis.SynthesizedResponse,
            sw.ElapsedMilliseconds
        );
    }

    /// <summary>
    /// Build execution plan based on intent and agent capabilities
    /// </summary>
    private Task<ExecutionPlan> BuildExecutionPlanAsync(
        string request,
        UserIntent intent,
        CoordinationContext context,
        CancellationToken cancellationToken
    )
    {
        using var activity = ActivitySource.StartActivity("Coordination.BuildPlan");

        var steps = new List<ExecutionStep>();
        var stepOrder = 0;

        // Determine which agents are needed based on intent
        var requiredAgents = DetermineRequiredAgents(intent);

        logger.LogInformation(
            "Required agents for intent {Intent}: {Agents}",
            intent.PrimaryIntent,
            string.Join(", ", requiredAgents.Select(a => a.Name))
        );

        // Build dependency graph
        var agentDependencies = BuildDependencyGraph(requiredAgents);

        // Create steps with dependencies
        foreach (var agent in requiredAgents)
        {
            var step = new ExecutionStep
            {
                Order = ++stepOrder,
                SubAgentName = agent.Name,
                Task = GenerateTaskDescription(agent, intent, request),
                DependsOn = agentDependencies.GetValueOrDefault(agent.Name, []),
            };

            steps.Add(step);
        }

        var plan = new ExecutionPlan
        {
            ConversationId = context.ConversationId,
            UserRequest = request,
            IntentSummary = intent.PrimaryIntent.ToString(),
            Steps = steps,
            Strategy = steps.Count > 2 ? PlanStrategy.Adaptive : PlanStrategy.Sequential,
        };

        return Task.FromResult(plan);
    }

    /// <summary>
    /// Execute a single step and collect all events
    /// </summary>
    private async Task<List<CoordinationEvent>> ExecuteStepWithEventsAsync(
        ExecutionStep step,
        CoordinationContext context,
        int totalSteps,
        CancellationToken cancellationToken
    )
    {
        var events = new List<CoordinationEvent>();

        using var activity = ActivitySource.StartActivity($"Coordination.Step.{step.SubAgentName}");
        activity?.SetTag("step_id", step.StepId);
        activity?.SetTag("agent", step.SubAgentName);

        events.Add(CoordinationEventFactory.StepStarted(step, totalSteps));
        context.CurrentPlan?.StartStep(step.StepId);

        AgentInvocations.Add(1, new KeyValuePair<string, object?>("agent", step.SubAgentName));

        if (!_agentsByName.TryGetValue(step.SubAgentName, out var agent))
        {
            logger.LogWarning("Agent {Agent} not found", step.SubAgentName);
            context.CurrentPlan?.FailStep(step.StepId, $"Agent {step.SubAgentName} not available");
            events.Add(
                CoordinationEventFactory.Error(
                    $"Agent {step.SubAgentName} not available",
                    step.StepId,
                    step.SubAgentName
                )
            );
            return events;
        }

        // Generate briefing for the agent
        var briefing = context.GenerateBriefing(step.SubAgentName, step);

        logger.LogInformation(
            "Executing step {StepId} with {Agent}. Briefing: {FactCount} facts",
            step.StepId,
            agent.Name,
            briefing.RelevantFacts.Count
        );

        SubAgentFindingV2 finding;

        try
        {
            // Check if agent supports coordinated requests
            if (agent is BaseCoordinatedSubAgent coordinatedAgent)
            {
                finding = await coordinatedAgent.HandleCoordinatedRequestAsync(
                    step.Task,
                    context.ConversationId,
                    briefing,
                    cancellationToken
                );
            }
            else
            {
                // Fallback for legacy agents
                var response = await agent.HandleRequestAsync(
                    step.Task,
                    context.ConversationId,
                    new Dictionary<string, object>
                    {
                        ["coordination_briefing"] = briefing.ToPromptContext(),
                    }
                );

                finding = new SubAgentFindingV2
                {
                    StepId = step.StepId,
                    SubAgentName = agent.Name,
                    TaskDescription = step.Task,
                    Status = response.Success ? FindingStatus.Success : FindingStatus.Error,
                    Summary = response.Result ?? "No response",
                    RawResponse = response.Result,
                    DurationMs = response.DurationMs,
                    ToolsUsed = response.ToolsUsed,
                };
            }

            // Update context with findings
            context.AddFinding(finding);
            context.CurrentPlan?.CompleteStep(step.StepId, finding);

            // Emit fact discovery events
            foreach (var fact in finding.DiscoveredFacts)
            {
                events.Add(CoordinationEventFactory.FactDiscovered(fact, step.StepId));
            }

            // Check for conflicts
            DetectConflicts(finding, context);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            context.CurrentPlan?.FailStep(step.StepId, ex.Message);
            finding = new SubAgentFindingV2
            {
                StepId = step.StepId,
                SubAgentName = agent.Name,
                TaskDescription = step.Task,
                Status = FindingStatus.Error,
                Summary = $"Error: {ex.Message}",
            };
            context.AddFinding(finding);

            logger.LogError(ex, "Step {StepId} failed: {Error}", step.StepId, ex.Message);
            events.Add(CoordinationEventFactory.Error(ex.Message, step.StepId, step.SubAgentName));
        }

        events.Add(CoordinationEventFactory.StepCompleted(step, totalSteps));
        return events;
    }

    /// <summary>
    /// Detect conflicts between this finding and previous findings
    /// </summary>
    private void DetectConflicts(SubAgentFindingV2 finding, CoordinationContext context)
    {
        foreach (var recommendation in finding.Recommendations)
        {
            foreach (
                var previousFinding in context
                    .GetAllFindings()
                    .Where(f => f.SubAgentName != finding.SubAgentName)
            )
            {
                foreach (var prevRec in previousFinding.Recommendations)
                {
                    if (AreContradictory(recommendation, prevRec))
                    {
                        var conflict = new ConflictRecord
                        {
                            ConflictingRecommendationIds =
                            [
                                recommendation.RecommendationId,
                                prevRec.RecommendationId,
                            ],
                            InvolvedAgents = [previousFinding.SubAgentName, finding.SubAgentName],
                            Description =
                                $"Conflicting recommendations: '{prevRec.Description}' vs '{recommendation.Description}'",
                        };

                        context.AddConflict(conflict);
                        ConflictCounter.Add(1);

                        logger.LogInformation(
                            "Conflict detected between {Agent1} and {Agent2}: {Description}",
                            previousFinding.SubAgentName,
                            finding.SubAgentName,
                            conflict.Description
                        );
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check if two recommendations are contradictory
    /// </summary>
    private static bool AreContradictory(Recommendation a, Recommendation b)
    {
        var opposingPairs = new[]
        {
            ("buy", "sell"),
            ("increase", "decrease"),
            ("approve", "reject"),
            ("proceed", "stop"),
            ("aggressive", "conservative"),
            ("high risk", "low risk"),
        };

        var aLower = a.Description.ToLowerInvariant();
        var bLower = b.Description.ToLowerInvariant();

        foreach (var (pos, neg) in opposingPairs)
        {
            if (
                (aLower.Contains(pos) && bLower.Contains(neg))
                || (aLower.Contains(neg) && bLower.Contains(pos))
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolve conflicts between agents and collect events
    /// </summary>
    private async Task<List<CoordinationEvent>> ResolveConflictsWithEventsAsync(
        IReadOnlyList<ConflictRecord> conflicts,
        CoordinationContext context,
        CancellationToken cancellationToken
    )
    {
        var events = new List<CoordinationEvent>();

        foreach (var conflict in conflicts.Where(c => !c.IsResolved))
        {
            var resolution = await ResolveConflictWithAIAsync(conflict, context, cancellationToken);

            var conflictResolution = new ConflictResolution
            {
                Method = ConflictResolutionMethod.MasterDecision,
                ResolvedBy = "CoordinationOrchestrator",
                Rationale = resolution.Rationale,
                WinningRecommendationId = null,
            };

            context.ResolveConflict(conflict.ConflictId, conflictResolution);

            events.Add(
                new CoordinationEvent
                {
                    Type = CoordinationEventType.ConflictResolved,
                    Content = $"Resolved: {resolution.Summary}",
                    Conflict = conflict with { Resolution = conflictResolution },
                }
            );
        }

        return events;
    }

    /// <summary>
    /// Use AI to resolve a conflict
    /// </summary>
    private async Task<(string Summary, string Rationale)> ResolveConflictWithAIAsync(
        ConflictRecord conflict,
        CoordinationContext context,
        CancellationToken cancellationToken
    )
    {
        var prompt =
            $@"You are resolving a conflict between expert agents.

CONFLICT:
{conflict.Description}

INVOLVED AGENTS: {string.Join(", ", conflict.InvolvedAgents)}

SHARED CONTEXT:
{string.Join("\n", context.GetAllFacts().Take(10).Select(f => $"- {f.Key}: {f.Value}"))}

Provide a balanced resolution. Format:
RESOLUTION: [your resolution in one sentence]
RATIONALE: [your reasoning]";

        var response = await chatClient.GetResponseAsync(
            prompt,
            cancellationToken: cancellationToken
        );
        var text = response.Text ?? "";

        var resolutionMatch = Regex.Match(
            text,
            @"RESOLUTION:\s*(.+?)(?=RATIONALE:|$)",
            RegexOptions.Singleline
        );
        var rationaleMatch = Regex.Match(text, @"RATIONALE:\s*(.+)$", RegexOptions.Singleline);

        return (resolutionMatch.Groups[1].Value.Trim(), rationaleMatch.Groups[1].Value.Trim());
    }

    /// <summary>
    /// Synthesize final response from all findings
    /// </summary>
    private async Task<CoordinationSynthesis> SynthesizeResponseAsync(
        CoordinationContext context,
        string originalRequest,
        CancellationToken cancellationToken
    )
    {
        using var activity = ActivitySource.StartActivity("Coordination.Synthesize");

        var findings = context.GetAllFindings();
        var facts = context.GetAllFacts();
        var conflicts = context.GetAllConflicts();

        var prompt =
            $@"Synthesize responses from multiple expert agents into a unified response.

ORIGINAL REQUEST:
{originalRequest}

AGENT FINDINGS:
{string.Join("\n\n", findings.Select(f => $"### {f.SubAgentName}\nStatus: {f.Status}\n{f.Summary}"))}

KEY FACTS:
{string.Join("\n", facts.Take(15).Select(f => $"- {f.Key}: {f.Value}"))}

{(conflicts.Any(c => c.IsResolved) ? $@"RESOLVED CONFLICTS:
{string.Join("\n", conflicts.Where(c => c.IsResolved).Select(c => $"- {c.Description}\n  Resolution: {c.Resolution?.Rationale}"))}" : "")}

Create a natural, conversational response that:
1. Directly answers the user's question
2. Incorporates relevant agent insights
3. Is clear and actionable";

        var response = await chatClient.GetResponseAsync(
            prompt,
            cancellationToken: cancellationToken
        );

        var allRecommendations = findings
            .SelectMany(f => f.Recommendations)
            .OrderByDescending(r => r.Confidence)
            .ToList();

        var allConcerns = findings
            .SelectMany(f => f.Concerns)
            .OrderByDescending(c => c.Severity)
            .ToList();

        return new CoordinationSynthesis
        {
            SynthesizedResponse = response.Text ?? "Unable to synthesize response",
            ContributingAgents = findings.Select(f => f.SubAgentName).ToList(),
            KeyFacts = facts.Take(10).ToList(),
            AggregatedRecommendations = allRecommendations,
            AggregatedConcerns = allConcerns,
            ResolvedConflicts = conflicts.Where(c => c.IsResolved).ToList(),
            OverallConfidence = CalculateOverallConfidence(findings, conflicts),
        };
    }

    private static double CalculateOverallConfidence(
        IReadOnlyList<SubAgentFindingV2> findings,
        IReadOnlyList<ConflictRecord> conflicts
    )
    {
        if (findings.Count == 0)
            return 0;

        // Base confidence from successful findings
        var successRate =
            (double)findings.Count(f => f.Status == FindingStatus.Success) / findings.Count;
        var unresolvedConflicts = conflicts.Count(c => !c.IsResolved);
        var conflictPenalty = unresolvedConflicts * 0.1;

        return Math.Max(0, Math.Min(1, successRate - conflictPenalty));
    }

    /// <summary>
    /// Determine which agents are needed based on intent
    /// </summary>
    private List<ISubAgent> DetermineRequiredAgents(UserIntent intent)
    {
        var agents = new List<ISubAgent>();

        // Use the RequiredSubAgents from intent classification if available
        if (intent.RequiredSubAgents.Count > 0)
        {
            foreach (var name in intent.RequiredSubAgents)
            {
                if (_agentsByName.TryGetValue(name, out var agent))
                {
                    agents.Add(agent);
                }
            }
        }

        // Fallback mapping for common intents
        if (agents.Count == 0)
        {
            var intentAgentMap = new Dictionary<IntentType, string[]>
            {
                [IntentType.PortfolioAnalysis] = ["PortfolioManager"],
                [IntentType.PortfolioCreation] =
                [
                    "AccountServices",
                    "ComplianceOfficer",
                    "PortfolioManager",
                ],
                [IntentType.InvestmentAdvice] = ["InvestmentAdvisor", "PortfolioManager"],
                [IntentType.RiskAssessment] = ["InvestmentAdvisor", "ComplianceOfficer"],
                [IntentType.AccountBalance] = ["AccountServices"],
                [IntentType.ComplianceCheck] = ["ComplianceOfficer"],
                [IntentType.ProfitProjection] = ["ProfitProjection", "PortfolioManager"],
                [IntentType.GeneralInquiry] = ["PortfolioManager"],
                [IntentType.Unknown] = ["PortfolioManager"],
            };

            var agentNames = intentAgentMap.GetValueOrDefault(
                intent.PrimaryIntent,
                ["PortfolioManager"]
            );

            foreach (var name in agentNames)
            {
                if (_agentsByName.TryGetValue(name, out var agent))
                {
                    agents.Add(agent);
                }
            }
        }

        // Always include at least one agent
        if (agents.Count == 0 && _agentsByName.Count > 0)
        {
            agents.Add(_agentsByName.Values.First());
        }

        return agents;
    }

    /// <summary>
    /// Build dependency graph between agents
    /// </summary>
    private static Dictionary<string, List<string>> BuildDependencyGraph(List<ISubAgent> agents)
    {
        var dependencies = new Dictionary<string, List<string>>();

        // Define typical dependencies
        var depRules = new Dictionary<string, string[]>
        {
            ["ComplianceOfficer"] = ["AccountServices"], // Compliance needs account info first
            ["PortfolioManager"] = ["AccountServices"], // Portfolio needs account data
            ["InvestmentAdvisor"] = ["PortfolioManager"], // Advisor needs portfolio context
        };

        var agentNames = agents.Select(a => a.Name).ToHashSet();

        foreach (var agent in agents)
        {
            var deps = new List<string>();

            if (depRules.TryGetValue(agent.Name, out var requiredDeps))
            {
                deps.AddRange(requiredDeps.Where(d => agentNames.Contains(d)));
            }

            dependencies[agent.Name] = deps;
        }

        return dependencies;
    }

    /// <summary>
    /// Generate task description for an agent based on their role and the intent
    /// </summary>
    private static string GenerateTaskDescription(
        ISubAgent agent,
        UserIntent intent,
        string request
    )
    {
        // Extract key entities from intent using proper entity type constants
        var amount = intent.GetEntityValue(EntityTypes.Amount, 0.0);
        var riskLevel = intent.GetEntityValue(EntityTypes.RiskLevel, "moderate");
        var investmentType = intent.GetEntityValue(EntityTypes.InvestmentType, "mutual fund");

        // Generate agent-specific task instructions
        return agent.Name switch
        {
            "AccountServices" => $"""
                TASK: Verify account status and eligibility for investment

                Customer Request: {request}

                You MUST use your tools to:
                1. Look up the customer's account information
                2. Verify account is active and in good standing
                3. Check if the account has sufficient balance or credit for SAR {amount:N0}
                4. Verify the customer's identity and account ownership

                If no account ID is provided, use a demo account or ask which account to verify.
                Report your findings clearly: account status, verification result, any issues found.
                """,

            "ComplianceOfficer" => $"""
                TASK: Perform compliance checks for a SAR {amount:N0} {investmentType} investment

                Customer Request: {request}
                Risk Profile: {riskLevel}

                You MUST verify:
                1. KYC (Know Your Customer) requirements are met
                2. AML (Anti-Money Laundering) screening is clear
                3. Investment amount is within regulatory limits
                4. Risk profile ({riskLevel}) is appropriate for the investment type
                5. Any suitability concerns for a {riskLevel} investor

                Use your compliance verification tools and report:
                - Compliance status (APPROVED / PENDING / REJECTED)
                - Any flags or concerns
                - Required documentation if any
                """,

            "InvestmentAdvisor" => $"""
                TASK: Provide investment recommendations for SAR {amount:N0}

                Customer Request: {request}
                Risk Profile: {riskLevel}
                Investment Type Preference: {investmentType}

                You MUST:
                1. Search for suitable {investmentType} options matching {riskLevel} risk profile
                2. Recommend specific funds with their details (NAV, returns, expense ratio)
                3. Suggest an optimal allocation strategy
                4. Explain why these recommendations suit a {riskLevel} investor

                Use your fund search and recommendation tools.
                Provide concrete fund names, expected returns, and allocation percentages.
                """,

            "PortfolioManager" => $"""
                TASK: Create a portfolio for SAR {amount:N0} investment

                Customer Request: {request}
                Risk Profile: {riskLevel}
                Investment Type: {investmentType}

                You MUST:
                1. Create a new portfolio or update existing one
                2. Allocate the SAR {amount:N0} across recommended funds
                3. Set up proper diversification for {riskLevel} risk profile
                4. Execute the portfolio creation/update

                Use your portfolio management tools to actually create/modify the portfolio.
                Report the portfolio ID, allocations made, and confirmation.
                """,

            "ProfitProjection" => $"""
                TASK: Project returns for SAR {amount:N0} investment

                Customer Request: {request}
                Risk Profile: {riskLevel}

                Calculate expected returns for:
                1. 1-year projection
                2. 3-year projection
                3. 5-year projection

                Use your projection tools to calculate realistic returns based on {riskLevel} investments.
                Include best-case, expected, and worst-case scenarios.
                """,

            _ => $"""
                TASK: Analyze and respond to the following request

                Customer Request: {request}

                Investment Amount: SAR {amount:N0}
                Risk Profile: {riskLevel}
                Investment Type: {investmentType}

                Use your available tools to fulfill this request and provide a detailed response.
                """,
        };
    }
}

/// <summary>
/// Result of coordination synthesis
/// </summary>
public record CoordinationSynthesis
{
    public required string SynthesizedResponse { get; init; }
    public List<string> ContributingAgents { get; init; } = [];
    public List<SharedFact> KeyFacts { get; init; } = [];
    public List<Recommendation> AggregatedRecommendations { get; init; } = [];
    public List<Concern> AggregatedConcerns { get; init; } = [];
    public List<ConflictRecord> ResolvedConflicts { get; init; } = [];
    public double OverallConfidence { get; init; }
}
