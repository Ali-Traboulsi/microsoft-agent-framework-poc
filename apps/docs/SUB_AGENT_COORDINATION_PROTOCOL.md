# Sub-Agent Coordination Protocol - Implementation Plan

## Executive Summary

The **Sub-Agent Coordination Protocol** is a P1 improvement that introduces structured communication and coordination between sub-agents, enabling the master orchestrator to handle complex, multi-step, and multi-domain requests with significantly higher intelligence and consistency.

**Goal**: Make the master agent capable of answering even "dummy" questions with super-intelligent reasoning by ensuring sub-agents can share context, build upon each other's work, and coordinate their actions seamlessly.

---

## Current State Analysis

### What We Have Now

1. **Intent Classification Layer** ✅ - Classifies user intent with extracted entities
2. **Structured Context Store** ✅ - Persists conversation context, entities, and goals
3. **Chain-of-Thought Reasoning** ✅ - Generates reasoning steps for complex decisions
4. **Sub-Agents** - 6 specialized agents:
   - `PortfolioManager` - Portfolio management
   - `InvestmentAdvisor` - Investment recommendations
   - `AccountServices` - Account operations
   - `ComplianceOfficer` - Risk/compliance
   - `ProfitProjection` - Investment projections
   - `ExternalApiServices` - SNB Capital, Fund-In, CIF lookups

### Current Limitations

| Issue | Description | Impact |
|-------|-------------|--------|
| **Isolated Sub-Agents** | Sub-agents work independently with minimal knowledge of each other's actions | Duplicate work, inconsistent answers |
| **No Inter-Agent Communication** | Sub-agents cannot request information from each other | Incomplete analysis, missing data |
| **Sequential-Only Coordination** | Workflows execute agents in fixed order without dynamic routing | Inflexible, can't adapt to request complexity |
| **Context Loss Between Agents** | Context passed between agents is limited to text summaries | Loss of structured data, entity inconsistencies |
| **No Consensus Mechanism** | When multiple agents have opinions, there's no resolution | Conflicting recommendations |
| **No Handoff Protocol** | No formal way to hand off partial work to another agent | Incomplete task resolution |

---

## Proposed Architecture

### Core Components

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        Sub-Agent Coordination Protocol                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    CoordinationContext                               │   │
│  │  • SharedFacts (validated, citable data)                            │   │
│  │  • SubAgentFindings (structured results from each agent)            │   │
│  │  • DependencyGraph (who depends on whom)                            │   │
│  │  • ExecutionPlan (steps with dependencies)                          │   │
│  │  • Handoffs (pending work transfers)                                │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                        │
│                                    ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                  CoordinationOrchestrator                            │   │
│  │  • BuildExecutionPlan() - Dynamic DAG-based execution planning      │   │
│  │  • ExecuteWithCoordination() - Parallel/sequential smart execution  │   │
│  │  • ResolveConflicts() - Consensus when agents disagree              │   │
│  │  • SynthesizeResponse() - Combine agent outputs intelligently       │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                        │
│                    ┌───────────────┼───────────────┐                       │
│                    ▼               ▼               ▼                       │
│  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐           │
│  │  SubAgentProxy   │ │  SubAgentProxy   │ │  SubAgentProxy   │           │
│  │  (PortfolioMgr)  │ │  (Compliance)    │ │  (ExternalAPI)   │           │
│  │                  │ │                  │ │                  │           │
│  │  • Capabilities  │ │  • Capabilities  │ │  • Capabilities  │           │
│  │  • Dependencies  │ │  • Dependencies  │ │  • Dependencies  │           │
│  │  • RequestAgent()│ │  • RequestAgent()│ │  • RequestAgent()│           │
│  │  • CanHandle()   │ │  • CanHandle()   │ │  • CanHandle()   │           │
│  │  • GetFindings() │ │  • GetFindings() │ │  • GetFindings() │           │
│  └──────────────────┘ └──────────────────┘ └──────────────────┘           │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Implementation Plan

### Phase 1: Core Protocol Infrastructure (Foundation)

#### 1.1 Create Coordination Domain Models

**File**: `src/Core/Domain/Coordination/CoordinationModels.cs`

```csharp
namespace AgentFrameworkQuickStart.Core.Domain.Coordination;

/// <summary>
/// Represents a verified fact that sub-agents can reference
/// </summary>
public record SharedFact
{
    public required string FactId { get; init; }
    public required string Category { get; init; } // e.g., "customer_data", "portfolio", "compliance"
    public required string Key { get; init; }
    public required object Value { get; init; }
    public required string SourceAgent { get; init; }
    public required string SourceTool { get; init; }
    public double Confidence { get; init; } = 1.0;
    public DateTime DiscoveredAt { get; init; } = DateTime.UtcNow;
    public TimeSpan? TimeToLive { get; init; } // null = never expires
    public bool IsExpired => TimeToLive.HasValue && DateTime.UtcNow - DiscoveredAt > TimeToLive;
}

/// <summary>
/// Structured result from a sub-agent's analysis
/// </summary>
public record SubAgentFindingV2
{
    public required string FindingId { get; init; }
    public required string SubAgentName { get; init; }
    public required string TaskDescription { get; init; }
    public required FindingStatus Status { get; init; }
    
    // Structured outputs
    public List<SharedFact> DiscoveredFacts { get; init; } = [];
    public List<Recommendation> Recommendations { get; init; } = [];
    public List<Concern> Concerns { get; init; } = [];
    public List<PendingQuestion> QuestionsForOtherAgents { get; init; } = [];
    
    // Execution info
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
    public long DurationMs { get; init; }
    public List<string> ToolsUsed { get; init; } = [];
}

public enum FindingStatus
{
    Success,
    PartialSuccess,
    NeedsMoreInfo,
    Blocked,
    Error
}

/// <summary>
/// A recommendation from a sub-agent
/// </summary>
public record Recommendation
{
    public required string RecommendationId { get; init; }
    public required string Description { get; init; }
    public required RecommendationType Type { get; init; }
    public double Confidence { get; init; }
    public string? Rationale { get; init; }
    public List<string> SupportingFacts { get; init; } = []; // FactIds
    public List<string> ConflictsWith { get; init; } = []; // Other RecommendationIds
}

public enum RecommendationType
{
    Action,      // "Do this"
    Caution,     // "Be careful about this"
    Information, // "FYI"
    Alternative  // "Consider this instead"
}

/// <summary>
/// A concern raised by a sub-agent
/// </summary>
public record Concern
{
    public required string ConcernId { get; init; }
    public required string Description { get; init; }
    public required ConcernSeverity Severity { get; init; }
    public required string Category { get; init; } // "compliance", "risk", "data_quality"
    public bool Blocking { get; init; }
    public string? Resolution { get; init; }
    public List<string> AffectedRecommendations { get; init; } = [];
}

public enum ConcernSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// A question one sub-agent has for another
/// </summary>
public record PendingQuestion
{
    public required string QuestionId { get; init; }
    public required string FromAgent { get; init; }
    public required string ToAgent { get; init; }
    public required string Question { get; init; }
    public required string Context { get; init; }
    public bool IsBlocking { get; init; }
    public string? Answer { get; init; }
    public DateTime AskedAt { get; init; } = DateTime.UtcNow;
    public DateTime? AnsweredAt { get; init; }
}
```

#### 1.2 Create Execution Plan Models

**File**: `src/Core/Domain/Coordination/ExecutionPlan.cs`

```csharp
namespace AgentFrameworkQuickStart.Core.Domain.Coordination;

/// <summary>
/// DAG-based execution plan for coordinated sub-agent work
/// </summary>
public class ExecutionPlan
{
    public required string PlanId { get; init; }
    public required string ConversationId { get; init; }
    public required string UserRequest { get; init; }
    public List<ExecutionStep> Steps { get; init; } = [];
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public PlanStrategy Strategy { get; init; }
    
    /// <summary>
    /// Get steps that can execute in parallel (no dependencies or all dependencies met)
    /// </summary>
    public IEnumerable<ExecutionStep> GetExecutableSteps()
    {
        var completedStepIds = Steps
            .Where(s => s.Status == StepStatus.Completed)
            .Select(s => s.StepId)
            .ToHashSet();
            
        return Steps.Where(s => 
            s.Status == StepStatus.Pending && 
            s.DependsOn.All(d => completedStepIds.Contains(d)));
    }
    
    /// <summary>
    /// Check if plan is complete
    /// </summary>
    public bool IsComplete => Steps.All(s => 
        s.Status == StepStatus.Completed || s.Status == StepStatus.Skipped);
}

public enum PlanStrategy
{
    Sequential,     // One at a time
    Parallel,       // All at once
    Adaptive,       // Dynamic based on dependencies
    Hierarchical    // Master reviews sub-agent work
}

/// <summary>
/// A single step in the execution plan
/// </summary>
public class ExecutionStep
{
    public required string StepId { get; init; }
    public required int Order { get; init; }
    public required string SubAgentName { get; init; }
    public required string Task { get; init; }
    public required string Rationale { get; init; }
    
    // Dependencies
    public List<string> DependsOn { get; init; } = [];
    public List<string> RequiredFacts { get; init; } = []; // FactIds needed before execution
    
    // Execution
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public SubAgentFindingV2? Result { get; set; }
    public long? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Adaptation
    public bool CanBeParallelized { get; init; }
    public bool IsCriticalPath { get; init; }
    public int? MaxRetries { get; init; } = 2;
    public int RetryCount { get; set; }
}

public enum StepStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped,
    Blocked
}
```

#### 1.3 Create Coordination Context

**File**: `src/Core/Domain/Coordination/CoordinationContext.cs`

```csharp
namespace AgentFrameworkQuickStart.Core.Domain.Coordination;

/// <summary>
/// Shared context for coordinated sub-agent execution
/// Thread-safe container for all coordination state
/// </summary>
public class CoordinationContext
{
    public required string ContextId { get; init; }
    public required string ConversationId { get; init; }
    public ExecutionPlan? CurrentPlan { get; set; }
    
    // Thread-safe collections
    private readonly object _lock = new();
    private readonly Dictionary<string, SharedFact> _facts = new();
    private readonly List<SubAgentFindingV2> _findings = [];
    private readonly List<PendingQuestion> _questions = [];
    private readonly List<ConflictRecord> _conflicts = [];
    
    /// <summary>
    /// Add a discovered fact (thread-safe)
    /// </summary>
    public void AddFact(SharedFact fact)
    {
        lock (_lock)
        {
            _facts[fact.FactId] = fact;
        }
    }
    
    /// <summary>
    /// Get a fact by ID
    /// </summary>
    public SharedFact? GetFact(string factId)
    {
        lock (_lock)
        {
            return _facts.TryGetValue(factId, out var fact) ? fact : null;
        }
    }
    
    /// <summary>
    /// Get all facts matching a category
    /// </summary>
    public IReadOnlyList<SharedFact> GetFactsByCategory(string category)
    {
        lock (_lock)
        {
            return _facts.Values
                .Where(f => f.Category == category && !f.IsExpired)
                .ToList();
        }
    }
    
    /// <summary>
    /// Add a sub-agent finding
    /// </summary>
    public void AddFinding(SubAgentFindingV2 finding)
    {
        lock (_lock)
        {
            _findings.Add(finding);
            // Also add any facts from the finding
            foreach (var fact in finding.DiscoveredFacts)
            {
                _facts[fact.FactId] = fact;
            }
        }
    }
    
    /// <summary>
    /// Get findings for a specific agent
    /// </summary>
    public IReadOnlyList<SubAgentFindingV2> GetFindingsForAgent(string agentName)
    {
        lock (_lock)
        {
            return _findings.Where(f => f.SubAgentName == agentName).ToList();
        }
    }
    
    /// <summary>
    /// Ask a question to another agent
    /// </summary>
    public string AskQuestion(string fromAgent, string toAgent, string question, string context, bool blocking = false)
    {
        var q = new PendingQuestion
        {
            QuestionId = Guid.NewGuid().ToString("N")[..8],
            FromAgent = fromAgent,
            ToAgent = toAgent,
            Question = question,
            Context = context,
            IsBlocking = blocking
        };
        
        lock (_lock)
        {
            _questions.Add(q);
        }
        
        return q.QuestionId;
    }
    
    /// <summary>
    /// Get unanswered questions for an agent
    /// </summary>
    public IReadOnlyList<PendingQuestion> GetPendingQuestionsFor(string agentName)
    {
        lock (_lock)
        {
            return _questions
                .Where(q => q.ToAgent == agentName && q.Answer == null)
                .ToList();
        }
    }
    
    /// <summary>
    /// Answer a pending question
    /// </summary>
    public void AnswerQuestion(string questionId, string answer)
    {
        lock (_lock)
        {
            var question = _questions.FirstOrDefault(q => q.QuestionId == questionId);
            if (question != null)
            {
                var index = _questions.IndexOf(question);
                _questions[index] = question with 
                { 
                    Answer = answer, 
                    AnsweredAt = DateTime.UtcNow 
                };
            }
        }
    }
    
    /// <summary>
    /// Record a conflict between recommendations
    /// </summary>
    public void RecordConflict(ConflictRecord conflict)
    {
        lock (_lock)
        {
            _conflicts.Add(conflict);
        }
    }
    
    /// <summary>
    /// Get all recommendations across all findings
    /// </summary>
    public IReadOnlyList<Recommendation> GetAllRecommendations()
    {
        lock (_lock)
        {
            return _findings.SelectMany(f => f.Recommendations).ToList();
        }
    }
    
    /// <summary>
    /// Get all concerns across all findings
    /// </summary>
    public IReadOnlyList<Concern> GetAllConcerns()
    {
        lock (_lock)
        {
            return _findings.SelectMany(f => f.Concerns).ToList();
        }
    }
    
    /// <summary>
    /// Generate a briefing for a sub-agent with relevant context
    /// </summary>
    public SubAgentCoordinationBriefing GenerateBriefing(string forAgent, ExecutionStep step)
    {
        lock (_lock)
        {
            // Get facts relevant to this agent's task
            var relevantFacts = _facts.Values
                .Where(f => !f.IsExpired)
                .Where(f => step.RequiredFacts.Contains(f.FactId) || IsRelevantToAgent(f, forAgent))
                .ToList();
            
            // Get findings from dependent steps
            var dependentFindings = _findings
                .Where(f => step.DependsOn.Any(d => 
                    CurrentPlan?.Steps.FirstOrDefault(s => s.StepId == d)?.SubAgentName == f.SubAgentName))
                .ToList();
            
            // Get questions this agent needs to answer
            var questionsToAnswer = _questions
                .Where(q => q.ToAgent == forAgent && q.Answer == null)
                .ToList();
            
            // Get answers to questions this agent asked
            var answeredQuestions = _questions
                .Where(q => q.FromAgent == forAgent && q.Answer != null)
                .ToList();
            
            return new SubAgentCoordinationBriefing
            {
                ForAgent = forAgent,
                Step = step,
                RelevantFacts = relevantFacts,
                PreviousFindings = dependentFindings,
                QuestionsToAnswer = questionsToAnswer,
                AnswersReceived = answeredQuestions,
                ActiveConcerns = _findings.SelectMany(f => f.Concerns).Where(c => !c.Blocking).ToList()
            };
        }
    }
    
    private static bool IsRelevantToAgent(SharedFact fact, string agentName)
    {
        // Define which fact categories are relevant to which agents
        var relevance = new Dictionary<string, HashSet<string>>
        {
            ["PortfolioManager"] = ["portfolio", "holdings", "customer_data", "account"],
            ["InvestmentAdvisor"] = ["funds", "market", "risk_profile", "customer_data"],
            ["AccountServices"] = ["account", "balance", "customer_data"],
            ["ComplianceOfficer"] = ["risk", "compliance", "customer_data", "portfolio"],
            ["ProfitProjection"] = ["portfolio", "funds", "market", "projection_params"],
            ["ExternalApiServices"] = ["customer_data", "cif", "api_response"]
        };
        
        return relevance.TryGetValue(agentName, out var categories) && 
               categories.Contains(fact.Category);
    }
}

/// <summary>
/// Briefing prepared for a sub-agent before it executes
/// </summary>
public record SubAgentCoordinationBriefing
{
    public required string ForAgent { get; init; }
    public required ExecutionStep Step { get; init; }
    public List<SharedFact> RelevantFacts { get; init; } = [];
    public List<SubAgentFindingV2> PreviousFindings { get; init; } = [];
    public List<PendingQuestion> QuestionsToAnswer { get; init; } = [];
    public List<PendingQuestion> AnswersReceived { get; init; } = [];
    public List<Concern> ActiveConcerns { get; init; } = [];
    
    /// <summary>
    /// Convert to a prompt-friendly string
    /// </summary>
    public string ToPromptContext()
    {
        var sb = new StringBuilder();
        
        if (RelevantFacts.Count > 0)
        {
            sb.AppendLine("## Known Facts (verified data you can reference)");
            foreach (var fact in RelevantFacts)
            {
                sb.AppendLine($"- **{fact.Key}**: {fact.Value} (from {fact.SourceAgent})");
            }
            sb.AppendLine();
        }
        
        if (PreviousFindings.Count > 0)
        {
            sb.AppendLine("## Previous Agent Work");
            foreach (var finding in PreviousFindings)
            {
                sb.AppendLine($"### {finding.SubAgentName}");
                sb.AppendLine($"Task: {finding.TaskDescription}");
                if (finding.Recommendations.Count > 0)
                {
                    sb.AppendLine("Recommendations:");
                    foreach (var rec in finding.Recommendations)
                    {
                        sb.AppendLine($"  - {rec.Description} ({rec.Confidence:P0} confidence)");
                    }
                }
            }
            sb.AppendLine();
        }
        
        if (QuestionsToAnswer.Count > 0)
        {
            sb.AppendLine("## Questions You Need to Answer");
            foreach (var q in QuestionsToAnswer)
            {
                sb.AppendLine($"- From {q.FromAgent}: {q.Question}");
            }
            sb.AppendLine();
        }
        
        if (ActiveConcerns.Count > 0)
        {
            sb.AppendLine("## Active Concerns");
            foreach (var concern in ActiveConcerns)
            {
                sb.AppendLine($"- [{concern.Severity}] {concern.Description}");
            }
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// Record of a conflict between agent recommendations
/// </summary>
public record ConflictRecord
{
    public required string ConflictId { get; init; }
    public required List<string> ConflictingRecommendationIds { get; init; }
    public required string Description { get; init; }
    public ConflictResolution? Resolution { get; set; }
}

public record ConflictResolution
{
    public required string ResolvedBy { get; init; } // "master_agent", "user", "consensus"
    public required string WinningRecommendationId { get; init; }
    public required string Rationale { get; init; }
    public DateTime ResolvedAt { get; init; } = DateTime.UtcNow;
}
```

---

### Phase 2: Coordination Orchestrator (Execution Engine)

#### 2.1 Create Coordination Orchestrator Interface

**File**: `src/Core/Interfaces/ICoordinationOrchestrator.cs`

```csharp
namespace AgentFrameworkQuickStart.Core.Interfaces;

using AgentFrameworkQuickStart.Core.Domain.Coordination;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;

/// <summary>
/// Orchestrates coordinated execution of multiple sub-agents
/// </summary>
public interface ICoordinationOrchestrator
{
    /// <summary>
    /// Build an execution plan for a user request
    /// </summary>
    Task<ExecutionPlan> BuildExecutionPlanAsync(
        UserIntent intent,
        ConversationContext conversationContext,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute a plan with full coordination
    /// </summary>
    Task<CoordinationResult> ExecuteWithCoordinationAsync(
        ExecutionPlan plan,
        CoordinationContext coordinationContext,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute a plan with streaming
    /// </summary>
    IAsyncEnumerable<CoordinationEvent> ExecuteWithCoordinationStreamingAsync(
        ExecutionPlan plan,
        CoordinationContext coordinationContext,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resolve conflicts between agent recommendations
    /// </summary>
    Task<ConflictResolution> ResolveConflictAsync(
        ConflictRecord conflict,
        CoordinationContext context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Synthesize final response from all agent findings
    /// </summary>
    Task<string> SynthesizeResponseAsync(
        CoordinationContext context,
        UserIntent originalIntent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of coordinated execution
/// </summary>
public record CoordinationResult
{
    public required string ResultId { get; init; }
    public required ExecutionPlan ExecutedPlan { get; init; }
    public required CoordinationContext FinalContext { get; init; }
    public required string SynthesizedResponse { get; init; }
    public List<ConflictRecord> ResolvedConflicts { get; init; } = [];
    public long TotalDurationMs { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Event emitted during coordinated execution
/// </summary>
public record CoordinationEvent
{
    public required CoordinationEventType Type { get; init; }
    public string? StepId { get; init; }
    public string? SubAgentName { get; init; }
    public string? Content { get; init; }
    public SubAgentFindingV2? Finding { get; init; }
    public ConflictRecord? Conflict { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public enum CoordinationEventType
{
    PlanCreated,
    StepStarted,
    StepProgress,
    StepCompleted,
    FactDiscovered,
    QuestionAsked,
    QuestionAnswered,
    ConflictDetected,
    ConflictResolved,
    SynthesisStarted,
    SynthesisCompleted,
    Error
}
```

#### 2.2 Implement Coordination Orchestrator

**File**: `src/Services/Coordination/CoordinationOrchestrator.cs`

```csharp
namespace AgentFrameworkQuickStart.Services.Coordination;

/// <summary>
/// Orchestrates coordinated execution of sub-agents with full context sharing
/// </summary>
public class CoordinationOrchestrator : ICoordinationOrchestrator
{
    private readonly IEnumerable<ISubAgent> _subAgents;
    private readonly IChatClient _chatClient;
    private readonly ILogger<CoordinationOrchestrator> _logger;
    private readonly Dictionary<string, ISubAgent> _subAgentLookup;
    
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.Coordination", "1.0.0");
    
    public CoordinationOrchestrator(
        IEnumerable<ISubAgent> subAgents,
        IChatClient chatClient,
        ILogger<CoordinationOrchestrator> logger)
    {
        _subAgents = subAgents;
        _chatClient = chatClient;
        _logger = logger;
        _subAgentLookup = subAgents.ToDictionary(sa => sa.Name, sa => sa);
    }
    
    public async Task<ExecutionPlan> BuildExecutionPlanAsync(
        UserIntent intent,
        ConversationContext conversationContext,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("BuildExecutionPlan");
        
        var plan = new ExecutionPlan
        {
            PlanId = Guid.NewGuid().ToString("N")[..8],
            ConversationId = conversationContext.ConversationId,
            UserRequest = intent.OriginalMessage,
            Strategy = DetermineStrategy(intent)
        };
        
        // Use LLM to build intelligent execution plan
        var planningPrompt = BuildPlanningPrompt(intent, conversationContext);
        var response = await _chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, planningPrompt)],
            new ChatOptions { Temperature = 0.2f },
            cancellationToken);
        
        // Parse LLM response into execution steps
        var steps = ParseExecutionSteps(response.Text ?? "");
        plan.Steps.AddRange(steps);
        
        _logger.LogInformation(
            "Built execution plan {PlanId} with {StepCount} steps, strategy: {Strategy}",
            plan.PlanId, plan.Steps.Count, plan.Strategy);
        
        return plan;
    }
    
    public async IAsyncEnumerable<CoordinationEvent> ExecuteWithCoordinationStreamingAsync(
        ExecutionPlan plan,
        CoordinationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ExecuteWithCoordination");
        
        yield return new CoordinationEvent
        {
            Type = CoordinationEventType.PlanCreated,
            Content = $"Executing plan with {plan.Steps.Count} steps using {plan.Strategy} strategy"
        };
        
        while (!plan.IsComplete)
        {
            var executableSteps = plan.GetExecutableSteps().ToList();
            
            if (executableSteps.Count == 0)
            {
                // Check for blocked steps
                var blockedSteps = plan.Steps.Where(s => s.Status == StepStatus.Pending).ToList();
                if (blockedSteps.Any())
                {
                    _logger.LogWarning("Plan has blocked steps: {Steps}", 
                        string.Join(", ", blockedSteps.Select(s => s.StepId)));
                    break;
                }
            }
            
            // Execute steps (parallel if strategy allows and no blocking questions)
            if (plan.Strategy == PlanStrategy.Parallel && executableSteps.Count > 1)
            {
                var tasks = executableSteps.Select(step => 
                    ExecuteStepAsync(step, context, cancellationToken));
                
                await foreach (var evt in MergeAsyncEnumerables(tasks))
                {
                    yield return evt;
                }
            }
            else
            {
                // Sequential execution
                foreach (var step in executableSteps)
                {
                    await foreach (var evt in ExecuteStepAsync(step, context, cancellationToken))
                    {
                        yield return evt;
                    }
                }
            }
            
            // Check for conflicts after steps complete
            var conflicts = DetectConflicts(context);
            foreach (var conflict in conflicts)
            {
                yield return new CoordinationEvent
                {
                    Type = CoordinationEventType.ConflictDetected,
                    Conflict = conflict
                };
                
                var resolution = await ResolveConflictAsync(conflict, context, cancellationToken);
                
                yield return new CoordinationEvent
                {
                    Type = CoordinationEventType.ConflictResolved,
                    Conflict = conflict,
                    Content = resolution.Rationale
                };
            }
            
            // Check for unanswered blocking questions
            await HandleBlockingQuestionsAsync(context, cancellationToken);
        }
        
        // Synthesize final response
        yield return new CoordinationEvent { Type = CoordinationEventType.SynthesisStarted };
        
        var response = await SynthesizeResponseAsync(context, /* intent */, cancellationToken);
        
        yield return new CoordinationEvent
        {
            Type = CoordinationEventType.SynthesisCompleted,
            Content = response
        };
    }
    
    private async IAsyncEnumerable<CoordinationEvent> ExecuteStepAsync(
        ExecutionStep step,
        CoordinationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        step.Status = StepStatus.InProgress;
        var sw = Stopwatch.StartNew();
        
        yield return new CoordinationEvent
        {
            Type = CoordinationEventType.StepStarted,
            StepId = step.StepId,
            SubAgentName = step.SubAgentName,
            Content = step.Task
        };
        
        try
        {
            if (!_subAgentLookup.TryGetValue(step.SubAgentName, out var subAgent))
            {
                throw new InvalidOperationException($"Sub-agent {step.SubAgentName} not found");
            }
            
            // Generate briefing with coordination context
            var briefing = context.GenerateBriefing(step.SubAgentName, step);
            var enrichedRequest = $"{briefing.ToPromptContext()}\n\n## Your Task\n{step.Task}";
            
            // Execute sub-agent with structured response extraction
            var response = await subAgent.HandleRequestAsync(
                enrichedRequest,
                context.ConversationId,
                new Dictionary<string, object>
                {
                    ["coordinationMode"] = true,
                    ["stepId"] = step.StepId
                });
            
            // Parse structured findings from response
            var finding = ExtractStructuredFinding(step, response);
            context.AddFinding(finding);
            
            step.Result = finding;
            step.Status = StepStatus.Completed;
            step.DurationMs = sw.ElapsedMilliseconds;
            
            // Emit discovered facts
            foreach (var fact in finding.DiscoveredFacts)
            {
                yield return new CoordinationEvent
                {
                    Type = CoordinationEventType.FactDiscovered,
                    StepId = step.StepId,
                    SubAgentName = step.SubAgentName,
                    Content = $"{fact.Key}: {fact.Value}"
                };
            }
            
            yield return new CoordinationEvent
            {
                Type = CoordinationEventType.StepCompleted,
                StepId = step.StepId,
                SubAgentName = step.SubAgentName,
                Finding = finding
            };
        }
        catch (Exception ex)
        {
            step.Status = StepStatus.Failed;
            step.ErrorMessage = ex.Message;
            
            yield return new CoordinationEvent
            {
                Type = CoordinationEventType.Error,
                StepId = step.StepId,
                SubAgentName = step.SubAgentName,
                Content = ex.Message
            };
            
            // Retry logic
            if (step.RetryCount < (step.MaxRetries ?? 0))
            {
                step.RetryCount++;
                step.Status = StepStatus.Pending;
                _logger.LogWarning("Retrying step {StepId}, attempt {Attempt}", 
                    step.StepId, step.RetryCount);
            }
        }
    }
    
    private PlanStrategy DetermineStrategy(UserIntent intent)
    {
        // Determine best strategy based on intent
        if (intent.RequiredSubAgents.Count <= 1)
            return PlanStrategy.Sequential;
            
        if (intent.Strategy == ExecutionStrategy.Parallel)
            return PlanStrategy.Parallel;
            
        if (intent.IsComposite)
            return PlanStrategy.Adaptive;
            
        return PlanStrategy.Sequential;
    }
    
    // ... additional helper methods
}
```

---

### Phase 3: Enhanced Sub-Agent Interface

#### 3.1 Extend ISubAgent Interface

**File**: Update `src/Api/Abstractions/ISubAgent.cs`

```csharp
namespace AgentFrameworkQuickStart.Api.Abstractions;

/// <summary>
/// Represents a specialized sub-agent that handles specific domain tasks
/// Extended with coordination protocol support
/// </summary>
public interface ISubAgent
{
    // ... existing members ...
    
    /// <summary>
    /// Declare what this agent can provide to others
    /// </summary>
    SubAgentCapabilities DeclaredCapabilities { get; }
    
    /// <summary>
    /// Handle request with coordination support
    /// </summary>
    Task<SubAgentFindingV2> HandleCoordinatedRequestAsync(
        string request,
        string conversationId,
        SubAgentCoordinationBriefing briefing,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Answer a question from another agent
    /// </summary>
    Task<string> AnswerQuestionAsync(
        PendingQuestion question,
        CoordinationContext context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if this agent can handle a specific task
    /// </summary>
    CapabilityMatch CanHandle(string taskDescription);
}

/// <summary>
/// Declared capabilities for coordination
/// </summary>
public record SubAgentCapabilities
{
    /// <summary>
    /// What data this agent can provide
    /// </summary>
    public List<string> ProvidesData { get; init; } = [];
    
    /// <summary>
    /// What data this agent needs from others
    /// </summary>
    public List<string> RequiresData { get; init; } = [];
    
    /// <summary>
    /// Tasks this agent specializes in
    /// </summary>
    public List<string> SpecializesIn { get; init; } = [];
    
    /// <summary>
    /// Other agents this typically works with
    /// </summary>
    public List<string> CollaboratesWith { get; init; } = [];
}

/// <summary>
/// Result of capability matching
/// </summary>
public record CapabilityMatch
{
    public bool CanHandle { get; init; }
    public double Confidence { get; init; }
    public string? Reason { get; init; }
    public List<string> MissingPrerequisites { get; init; } = [];
}
```

---

### Phase 4: Integration with Master Orchestrator

#### 4.1 Update MasterOrchestrator

**File**: Extend `src/Api/Orchestration/MasterOrchestrator.Coordination.cs`

```csharp
namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator - Coordination methods
/// </summary>
public partial class MasterOrchestrator
{
    private readonly ICoordinationOrchestrator _coordinationOrchestrator;
    
    /// <summary>
    /// Process complex requests using coordinated sub-agent execution
    /// </summary>
    public async IAsyncEnumerable<UnifiedStreamingChunk> ProcessWithCoordinationAsync(
        UserIntent intent,
        string conversationId,
        ConversationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Create coordination context
        var coordinationContext = new CoordinationContext
        {
            ContextId = Guid.NewGuid().ToString("N")[..8],
            ConversationId = conversationId
        };
        
        // Populate with existing context
        foreach (var entity in context.Entities.Values)
        {
            coordinationContext.AddFact(new SharedFact
            {
                FactId = Guid.NewGuid().ToString("N")[..8],
                Category = entity.EntityType,
                Key = entity.EntityType,
                Value = entity.NormalizedValue ?? entity.RawValue,
                SourceAgent = "IntentClassifier",
                SourceTool = "EntityExtraction",
                Confidence = entity.Confidence
            });
        }
        
        // Build execution plan
        yield return new UnifiedStreamingChunk
        {
            Type = StreamingChunkType.Thinking,
            Content = "Building execution plan...\n"
        };
        
        var plan = await _coordinationOrchestrator.BuildExecutionPlanAsync(
            intent, context, cancellationToken);
        
        yield return new UnifiedStreamingChunk
        {
            Type = StreamingChunkType.Thinking,
            Content = $"Plan: {plan.Steps.Count} steps, {plan.Strategy} strategy\n" +
                string.Join("\n", plan.Steps.Select(s => $"  {s.Order}. [{s.SubAgentName}] {s.Task}"))
        };
        
        // Execute with coordination
        await foreach (var evt in _coordinationOrchestrator.ExecuteWithCoordinationStreamingAsync(
            plan, coordinationContext, cancellationToken))
        {
            yield return ConvertCoordinationEvent(evt);
        }
    }
    
    private static UnifiedStreamingChunk ConvertCoordinationEvent(CoordinationEvent evt) =>
        evt.Type switch
        {
            CoordinationEventType.StepStarted => new UnifiedStreamingChunk
            {
                Type = StreamingChunkType.SubAgentDelegation,
                SubAgentName = evt.SubAgentName,
                Content = evt.Content
            },
            CoordinationEventType.StepCompleted => new UnifiedStreamingChunk
            {
                Type = StreamingChunkType.SubAgentComplete,
                SubAgentName = evt.SubAgentName
            },
            CoordinationEventType.FactDiscovered => new UnifiedStreamingChunk
            {
                Type = StreamingChunkType.Reasoning,
                Content = $"📊 Discovered: {evt.Content}\n"
            },
            CoordinationEventType.SynthesisCompleted => new UnifiedStreamingChunk
            {
                Type = StreamingChunkType.Content,
                Content = evt.Content
            },
            _ => new UnifiedStreamingChunk
            {
                Type = StreamingChunkType.Content,
                Content = evt.Content
            }
        };
}
```

---

### Phase 5: Response Synthesis Engine

#### 5.1 Create Response Synthesizer

**File**: `src/Services/Coordination/ResponseSynthesizer.cs`

```csharp
namespace AgentFrameworkQuickStart.Services.Coordination;

/// <summary>
/// Synthesizes coherent responses from multiple sub-agent findings
/// </summary>
public class ResponseSynthesizer
{
    private readonly IChatClient _chatClient;
    
    private const string SynthesisPrompt = """
        You are synthesizing a response from multiple specialist agents for a user.
        
        ## User's Original Request
        {USER_REQUEST}
        
        ## Agent Findings
        {AGENT_FINDINGS}
        
        ## Your Task
        Create a single, coherent, professional response that:
        1. Addresses the user's request directly
        2. Integrates insights from all agents naturally
        3. Highlights key recommendations with confidence levels
        4. Addresses any concerns or risks appropriately
        5. Uses professional but friendly language
        6. Formats with markdown for readability
        
        ## Response Guidelines
        - Lead with the most important information
        - Group related insights together
        - Use bullet points for lists
        - Include specific numbers/values from the facts
        - End with clear next steps or recommendations
        - If agents had concerns, address them appropriately
        
        Generate the synthesized response:
        """;
    
    public async Task<string> SynthesizeAsync(
        CoordinationContext context,
        UserIntent originalIntent,
        CancellationToken cancellationToken = default)
    {
        var findings = FormatFindings(context);
        
        var prompt = SynthesisPrompt
            .Replace("{USER_REQUEST}", originalIntent.OriginalMessage)
            .Replace("{AGENT_FINDINGS}", findings);
        
        var response = await _chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)],
            new ChatOptions { Temperature = 0.3f },
            cancellationToken);
        
        return response.Text ?? "Unable to synthesize response.";
    }
    
    private static string FormatFindings(CoordinationContext context)
    {
        var sb = new StringBuilder();
        
        var allFindings = context.GetAllFindings();
        foreach (var finding in allFindings)
        {
            sb.AppendLine($"### {finding.SubAgentName}");
            sb.AppendLine($"**Task:** {finding.TaskDescription}");
            sb.AppendLine($"**Status:** {finding.Status}");
            
            if (finding.DiscoveredFacts.Count > 0)
            {
                sb.AppendLine("**Discovered Facts:**");
                foreach (var fact in finding.DiscoveredFacts)
                {
                    sb.AppendLine($"- {fact.Key}: {fact.Value}");
                }
            }
            
            if (finding.Recommendations.Count > 0)
            {
                sb.AppendLine("**Recommendations:**");
                foreach (var rec in finding.Recommendations)
                {
                    sb.AppendLine($"- {rec.Description} ({rec.Confidence:P0} confidence)");
                    if (!string.IsNullOrEmpty(rec.Rationale))
                        sb.AppendLine($"  Rationale: {rec.Rationale}");
                }
            }
            
            if (finding.Concerns.Count > 0)
            {
                sb.AppendLine("**Concerns:**");
                foreach (var concern in finding.Concerns)
                {
                    sb.AppendLine($"- [{concern.Severity}] {concern.Description}");
                }
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
}
```

---

## Implementation Checklist

### Phase 1: Core Protocol Infrastructure
- [ ] Create `CoordinationModels.cs` with SharedFact, SubAgentFindingV2, etc.
- [ ] Create `ExecutionPlan.cs` with DAG-based execution model
- [ ] Create `CoordinationContext.cs` for shared state management
- [ ] Add unit tests for coordination models

### Phase 2: Coordination Orchestrator
- [ ] Create `ICoordinationOrchestrator.cs` interface
- [ ] Implement `CoordinationOrchestrator.cs`
- [ ] Implement execution plan builder with LLM
- [ ] Implement parallel/sequential execution logic
- [ ] Add conflict detection and resolution
- [ ] Add integration tests

### Phase 3: Enhanced Sub-Agent Interface
- [ ] Extend `ISubAgent` with coordination methods
- [ ] Update each sub-agent to implement new interface
- [ ] Add `DeclaredCapabilities` to each sub-agent
- [ ] Implement `HandleCoordinatedRequestAsync` for each sub-agent
- [ ] Add capability matching logic

### Phase 4: Master Orchestrator Integration
- [ ] Create `MasterOrchestrator.Coordination.cs`
- [ ] Integrate coordination flow into `ProcessAsync`
- [ ] Update streaming to include coordination events
- [ ] Add feature flag for coordination mode

### Phase 5: Response Synthesis
- [ ] Create `ResponseSynthesizer.cs`
- [ ] Implement multi-agent response integration
- [ ] Add conflict highlighting
- [ ] Add recommendation summarization

### Phase 6: Testing & Validation
- [ ] Create test scenarios for multi-agent coordination
- [ ] Test conflict resolution
- [ ] Test parallel execution
- [ ] Performance testing
- [ ] Update documentation

---

## Expected Benefits

| Capability | Before | After |
|------------|--------|-------|
| Multi-domain queries | Sequential, isolated | Parallel with shared context |
| Context sharing | Text summaries only | Structured facts with confidence |
| Conflict resolution | None | Automatic detection + LLM resolution |
| Agent handoffs | Manual | Structured with questions/answers |
| Response quality | Individual agent responses | Synthesized coherent response |
| Error recovery | Stop on failure | Retry + fallback + alternative agents |

---

## 🔴 CRITICAL DESIGN REQUIREMENTS

### 1. Real-Time Streaming (Non-Negotiable)

The coordination protocol MUST stream all events to the frontend in real-time:

```
┌─────────────────────────────────────────────────────────────────┐
│ USER SEES IN REAL-TIME:                                         │
├─────────────────────────────────────────────────────────────────┤
│ 🎯 Building execution plan...                                   │
│ 📋 Plan: 3 steps, Adaptive strategy                            │
│    1. [ComplianceOfficer] Verify user eligibility              │
│    2. [InvestmentAdvisor] Generate recommendations             │
│    3. [PortfolioManager] Create portfolio                      │
│                                                                 │
│ ▶️ Step 1: ComplianceOfficer starting...                        │
│   📊 Discovered: risk_profile = "moderate"                     │
│   📊 Discovered: max_equity_exposure = 60%                     │
│   ✅ Step 1 completed (245ms)                                  │
│                                                                 │
│ ▶️ Step 2: InvestmentAdvisor starting...                        │
│   💡 Using fact: risk_profile = "moderate"                     │
│   📊 Recommendation: AlAhli Multi-Asset Fund (85% confidence)  │
│   ✅ Step 2 completed (312ms)                                  │
│                                                                 │
│ ▶️ Step 3: PortfolioManager starting...                         │
│   ✅ Portfolio created: PORT-12345                             │
│                                                                 │
│ 🔄 Synthesizing response...                                    │
│ ✨ [Final coherent response streams here...]                   │
└─────────────────────────────────────────────────────────────────┘
```

**Implementation Requirement:**
- All `CoordinationEvent` types MUST map to `UnifiedStreamingChunk`
- Events MUST be yielded immediately (no buffering)
- SignalR integration MUST remain intact
- Frontend receives granular progress updates

### 2. Easy Sub-Agent Onboarding (Minimal Effort)

Adding a new sub-agent MUST require:
- **1 new file** - The sub-agent class
- **1 line in DI** - Registration in Program.cs
- **0 changes** to orchestrator, coordinator, or protocol

**Pattern: Inherit from BaseCoordinatedSubAgent**

```csharp
// Adding a new sub-agent should be THIS simple:
public class NewSubAgent : BaseCoordinatedSubAgent
{
    public override string Name => "NewAgent";
    public override string Domain => "New Domain";
    public override string[] Capabilities => ["capability1", "capability2"];
    
    // OPTIONAL: Override only what you need
    public override SubAgentCapabilityDeclaration DeclaredCapabilities => new()
    {
        ProvidesData = ["new_data_type"],
        RequiresData = ["customer_data"],
        SpecializesIn = ["new_task_type"]
    };
    
    // The base class handles ALL coordination protocol details
    // Just implement your tools and logic
}
```

**Design Principles:**
1. **Convention over Configuration** - Sensible defaults everywhere
2. **Opt-in Complexity** - Basic agents work without coordination knowledge
3. **Progressive Enhancement** - Add coordination features incrementally
4. **Backward Compatibility** - Existing sub-agents continue to work

---

## Example Scenario

**User asks:** "I want to invest 200,000 SAR for 3 years. Check if I'm compliant and recommend funds."

**Without Coordination:**
1. Master sends to InvestmentAdvisor → gets recommendations
2. Master sends to ComplianceOfficer → checks compliance (no knowledge of recommendations)
3. Responses are concatenated, may conflict

**With Coordination Protocol:**
1. Plan built: [Compliance → InvestmentAdvisor (depends on compliance)] or [parallel if no dependency]
2. Compliance discovers: `risk_profile: moderate, max_equity_exposure: 60%`
3. These facts shared with InvestmentAdvisor
4. InvestmentAdvisor uses facts to constrain recommendations
5. If InvestmentAdvisor suggests 70% equity fund → Conflict detected
6. Master resolves conflict with rationale
7. Final response synthesized: coherent, compliant, and well-reasoned

---

## Next Steps

1. **Review this plan** and confirm direction
2. **Start with Phase 1** - Core domain models (low risk, foundational)
3. **Iterate** - Build incrementally, testing each phase
4. **Feature flag** - Roll out gradually

Would you like me to begin implementing Phase 1?
