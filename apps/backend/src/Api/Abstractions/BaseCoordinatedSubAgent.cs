using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AgentFrameworkQuickStart.Core.Domain.Coordination;
using AgentFrameworkQuickStart.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Abstractions;

/// <summary>
/// Base class for sub-agents with built-in coordination protocol support.
/// New sub-agents should inherit from this class for automatic coordination integration.
///
/// Benefits:
/// - Automatic OpenTelemetry instrumentation
/// - Built-in coordination context handling
/// - Structured finding extraction
/// - Conversation memory management
/// - Minimal boilerplate for new agents
/// </summary>
public abstract class BaseCoordinatedSubAgent : ISubAgent
{
    // ===== REQUIRED OVERRIDES (minimal implementation) =====

    /// <summary>
    /// Unique name of the sub-agent. Override this.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Domain description. Override this.
    /// </summary>
    public abstract string Domain { get; }

    /// <summary>
    /// List of capabilities. Override this.
    /// </summary>
    public abstract string[] Capabilities { get; }

    /// <summary>
    /// Create the AI agent with tools. Override this.
    /// </summary>
    protected abstract AIAgent CreateAgent();

    // ===== OPTIONAL OVERRIDES (for advanced coordination) =====

    /// <summary>
    /// Declare what this agent provides and needs for coordination.
    /// Override to enable smart dependency resolution.
    /// </summary>
    public virtual SubAgentCapabilityDeclaration DeclaredCapabilities =>
        new()
        {
            ProvidesData = [],
            RequiresData = [],
            SpecializesIn = [],
            CollaboratesWith = [],
            CanRunInParallel = true,
            DomainAuthorityPriority = 5,
        };

    /// <summary>
    /// Extract structured facts from the agent's response.
    /// Override for domain-specific fact extraction.
    /// </summary>
    protected virtual List<SharedFact> ExtractFacts(string response, string taskDescription)
    {
        // Default: no automatic extraction, agents can add facts explicitly
        return [];
    }

    /// <summary>
    /// Extract recommendations from the agent's response.
    /// Override for domain-specific recommendation extraction.
    /// </summary>
    protected virtual List<Recommendation> ExtractRecommendations(string response)
    {
        // Default: no automatic extraction
        return [];
    }

    /// <summary>
    /// Extract concerns from the agent's response.
    /// Override for domain-specific concern extraction.
    /// </summary>
    protected virtual List<Concern> ExtractConcerns(string response)
    {
        // Default: no automatic extraction
        return [];
    }

    // ===== PROTECTED INFRASTRUCTURE (use in subclasses) =====

    protected readonly IChatClient ChatClient;
    protected readonly SubAgentThreadManager ThreadManager;
    protected readonly ILogger Logger;
    protected readonly Lazy<AIAgent> Agent;

    // OpenTelemetry
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestDuration;

    // ===== CONSTRUCTOR =====

    protected BaseCoordinatedSubAgent(
        IChatClient chatClient,
        SubAgentThreadManager threadManager,
        ILogger logger
    )
    {
        ChatClient = chatClient;
        ThreadManager = threadManager;
        Logger = logger;
        Agent = new Lazy<AIAgent>(CreateAgent);

        // Initialize telemetry with agent name
        _activitySource = new ActivitySource(
            $"InvestmentBanking.SubAgents.{GetType().Name}",
            "2.0.0"
        );
        _meter = new Meter($"InvestmentBanking.SubAgents.{GetType().Name}", "2.0.0");
        _requestCounter = _meter.CreateCounter<long>(
            "subagent.requests",
            description: "Number of requests handled"
        );
        _requestDuration = _meter.CreateHistogram<double>("subagent.request.duration", unit: "ms");
    }

    // ===== STANDARD REQUEST HANDLING (backward compatible) =====

    public virtual async Task<SubAgentResponse> HandleRequestAsync(
        string request,
        string conversationId,
        Dictionary<string, object>? context = null
    )
    {
        using var activity = _activitySource.StartActivity(
            $"{Name}.HandleRequest",
            ActivityKind.Internal
        );
        activity?.SetTag("subagent.name", Name);
        activity?.SetTag("request.length", request.Length);

        var sw = Stopwatch.StartNew();

        try
        {
            Logger.LogInformation(
                "{SubAgent} handling request for conversation {ConversationId}: {Request}",
                Name,
                conversationId,
                request.Length > 100 ? request[..100] + "..." : request
            );

            // Get shared conversation context
            var conversationContext = await ThreadManager.GetConversationContextAsync(
                conversationId
            );

            // Build full request with context
            var fullRequest = string.IsNullOrEmpty(conversationContext)
                ? request
                : $"{conversationContext}\n\nCurrent request: {request}";

            // Execute
            var thread = ThreadManager.GetOrCreateThread(conversationId, Name, Agent.Value);
            var result = await Agent.Value.RunAsync(fullRequest, thread);
            var responseText = result.Messages.LastOrDefault()?.Text ?? "No response generated";

            // Store in conversation memory
            ThreadManager.AddMemory(conversationId, Name, request, responseText);

            sw.Stop();
            RecordSuccess(sw.ElapsedMilliseconds, activity);

            return new SubAgentResponse
            {
                SubAgentName = Name,
                Success = true,
                Result = responseText,
                ToolsUsed = [],
                DurationMs = sw.ElapsedMilliseconds,
                Metadata = context ?? new(),
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordError(ex, sw.ElapsedMilliseconds, activity);

            return new SubAgentResponse
            {
                SubAgentName = Name,
                Success = false,
                ErrorMessage = ex.Message,
                DurationMs = sw.ElapsedMilliseconds,
                ToolsUsed = [],
                Metadata = context ?? new(),
            };
        }
    }

    // ===== STREAMING REQUEST HANDLING =====

    public virtual async IAsyncEnumerable<SubAgentStreamChunk> HandleRequestStreamingAsync(
        string request,
        string conversationId,
        Dictionary<string, object>? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        Logger.LogInformation(
            "{SubAgent} handling streaming request for conversation {ConversationId}",
            Name,
            conversationId
        );

        // Get shared conversation context
        var conversationContext = await ThreadManager.GetConversationContextAsync(conversationId);

        // Build full request with context
        var fullRequest = string.IsNullOrEmpty(conversationContext)
            ? request
            : $"{conversationContext}\n\nCurrent request: {request}";

        var thread = ThreadManager.GetOrCreateThread(conversationId, Name, Agent.Value);
        var responseBuilder = new StringBuilder();

        await foreach (var chunk in Agent.Value.RunStreamingAsync(fullRequest, thread))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (chunk.Text != null)
            {
                responseBuilder.Append(chunk.Text);
                yield return new SubAgentStreamChunk { Text = chunk.Text, IsComplete = false };
            }
        }

        // Store in conversation memory
        ThreadManager.AddMemory(conversationId, Name, request, responseBuilder.ToString());

        yield return new SubAgentStreamChunk { IsComplete = true };
    }

    // ===== COORDINATED REQUEST HANDLING (new protocol) =====

    /// <summary>
    /// Handle a request with full coordination protocol support.
    /// Uses the briefing to incorporate shared context and extracts structured findings.
    /// </summary>
    public virtual async Task<SubAgentFindingV2> HandleCoordinatedRequestAsync(
        string request,
        string conversationId,
        SubAgentCoordinationBriefing briefing,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = _activitySource.StartActivity(
            $"{Name}.HandleCoordinatedRequest",
            ActivityKind.Internal
        );
        activity?.SetTag("subagent.name", Name);
        activity?.SetTag("coordination.mode", true);
        activity?.SetTag("briefing.has_facts", briefing.RelevantFacts.Count > 0);

        var sw = Stopwatch.StartNew();
        var toolsUsed = new List<string>();

        try
        {
            Logger.LogInformation(
                "{SubAgent} handling coordinated request for conversation {ConversationId}. "
                    + "Briefing: {FactCount} facts, {FindingCount} previous findings, {QuestionCount} questions to answer",
                Name,
                conversationId,
                briefing.RelevantFacts.Count,
                briefing.PreviousFindings.Count,
                briefing.QuestionsToAnswer.Count
            );

            // Build enriched request with coordination context
            var enrichedRequest = BuildEnrichedRequest(request, briefing);

            // Execute
            var thread = ThreadManager.GetOrCreateThread(conversationId, Name, Agent.Value);
            var result = await Agent.Value.RunAsync(enrichedRequest, thread);
            var responseText = result.Messages.LastOrDefault()?.Text ?? "No response generated";

            // Store in conversation memory
            ThreadManager.AddMemory(conversationId, Name, request, responseText);

            sw.Stop();

            // Extract structured data from response
            var facts = ExtractFacts(responseText, briefing.Step.Task);
            var recommendations = ExtractRecommendations(responseText);
            var concerns = ExtractConcerns(responseText);

            RecordSuccess(sw.ElapsedMilliseconds, activity);

            return new SubAgentFindingV2
            {
                SubAgentName = Name,
                TaskDescription = briefing.Step.Task,
                Status = FindingStatus.Success,
                Summary = responseText.Length > 200 ? responseText[..200] + "..." : responseText,
                RawResponse = responseText,
                DiscoveredFacts = facts,
                Recommendations = recommendations,
                Concerns = concerns,
                DurationMs = sw.ElapsedMilliseconds,
                ToolsUsed = toolsUsed,
                StepId = briefing.Step.StepId,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordError(ex, sw.ElapsedMilliseconds, activity);

            return new SubAgentFindingV2
            {
                SubAgentName = Name,
                TaskDescription = briefing.Step.Task,
                Status = FindingStatus.Error,
                Summary = $"Error: {ex.Message}",
                DurationMs = sw.ElapsedMilliseconds,
                StepId = briefing.Step.StepId,
            };
        }
    }

    /// <summary>
    /// Answer a question from another agent
    /// </summary>
    public virtual async Task<string> AnswerQuestionAsync(
        PendingQuestion question,
        CoordinationContext context,
        CancellationToken cancellationToken = default
    )
    {
        Logger.LogInformation(
            "{SubAgent} answering question from {FromAgent}: {Question}",
            Name,
            question.FromAgent,
            question.Question
        );

        var prompt =
            $@"Another agent ({question.FromAgent}) has asked you a question:

Question: {question.Question}
{(question.Context != null ? $"Context: {question.Context}" : "")}

Please provide a concise, direct answer based on your expertise and any relevant facts you know.";

        var thread = ThreadManager.GetOrCreateThread(context.ConversationId, Name, Agent.Value);
        var result = await Agent.Value.RunAsync(prompt, thread);
        return result.Messages.LastOrDefault()?.Text ?? "Unable to answer";
    }

    /// <summary>
    /// Check if this agent can handle a specific task
    /// </summary>
    public virtual CapabilityMatch CanHandle(string taskDescription)
    {
        // Check if any capability keywords match
        var lowerTask = taskDescription.ToLowerInvariant();

        foreach (var capability in Capabilities)
        {
            var lowerCap = capability.ToLowerInvariant();
            var keywords = lowerCap.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var matchCount = keywords.Count(kw => lowerTask.Contains(kw));
            var matchRatio = (double)matchCount / keywords.Length;

            if (matchRatio > 0.5)
            {
                return CapabilityMatch.Yes(
                    confidence: matchRatio,
                    reason: $"Matches capability: {capability}"
                );
            }
        }

        // Check specializations
        foreach (var spec in DeclaredCapabilities.SpecializesIn)
        {
            if (lowerTask.Contains(spec.ToLowerInvariant()))
            {
                return CapabilityMatch.Yes(confidence: 0.9, reason: $"Specializes in: {spec}");
            }
        }

        return CapabilityMatch.No($"No matching capabilities for task: {taskDescription}");
    }

    // ===== HELPER METHODS =====

    /// <summary>
    /// Build an enriched request that includes coordination context
    /// </summary>
    protected virtual string BuildEnrichedRequest(
        string request,
        SubAgentCoordinationBriefing briefing
    )
    {
        var sb = new StringBuilder();

        // Add coordination context if available
        var contextString = briefing.ToPromptContext();
        if (!string.IsNullOrEmpty(contextString))
        {
            sb.AppendLine(contextString);
        }

        // Add the actual request
        sb.AppendLine("## Your Task");
        sb.AppendLine(request);

        // Add instruction to report facts
        sb.AppendLine();
        sb.AppendLine("## Output Instructions");
        sb.AppendLine("When you discover important facts, clearly state them.");
        sb.AppendLine("If you have recommendations, state them with confidence levels.");
        sb.AppendLine("If you have concerns, clearly flag them with severity.");

        return sb.ToString();
    }

    /// <summary>
    /// Create a SharedFact (helper for subclasses)
    /// </summary>
    protected SharedFact CreateFact(
        string category,
        string key,
        object value,
        string? sourceTool = null,
        double confidence = 1.0
    )
    {
        return new SharedFact
        {
            Category = category,
            Key = key,
            Value = value,
            SourceAgent = Name,
            SourceTool = sourceTool,
            Confidence = confidence,
        };
    }

    /// <summary>
    /// Create a Recommendation (helper for subclasses)
    /// </summary>
    protected Recommendation CreateRecommendation(
        string description,
        RecommendationType type = RecommendationType.Action,
        double confidence = 0.8,
        string? rationale = null
    )
    {
        return new Recommendation
        {
            Description = description,
            Type = type,
            Confidence = confidence,
            Rationale = rationale,
            SourceAgent = Name,
        };
    }

    /// <summary>
    /// Create a Concern (helper for subclasses)
    /// </summary>
    protected Concern CreateConcern(
        string description,
        ConcernSeverity severity,
        string category,
        bool blocking = false,
        string? resolution = null
    )
    {
        return new Concern
        {
            Description = description,
            Severity = severity,
            Category = category,
            Blocking = blocking,
            SuggestedResolution = resolution,
            SourceAgent = Name,
        };
    }

    private void RecordSuccess(long durationMs, Activity? activity)
    {
        _requestCounter.Add(
            1,
            new KeyValuePair<string, object?>("subagent", Name),
            new KeyValuePair<string, object?>("success", true)
        );
        _requestDuration.Record(durationMs, new KeyValuePair<string, object?>("subagent", Name));

        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("duration_ms", durationMs);

        Logger.LogInformation("{SubAgent} completed in {Duration}ms", Name, durationMs);
    }

    private void RecordError(Exception ex, long durationMs, Activity? activity)
    {
        _requestCounter.Add(
            1,
            new KeyValuePair<string, object?>("subagent", Name),
            new KeyValuePair<string, object?>("success", false)
        );

        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("error.type", ex.GetType().Name);

        Logger.LogError(
            ex,
            "{SubAgent} error after {Duration}ms: {Error}",
            Name,
            durationMs,
            ex.Message
        );
    }
}
