using System.Text;

namespace AgentFrameworkQuickStart.Core.Domain.Coordination;

/// <summary>
/// Thread-safe shared context for coordinated sub-agent execution.
/// This is the central hub for all coordination state.
/// </summary>
public class CoordinationContext
{
    private readonly object _lock = new();

    /// <summary>
    /// Unique identifier for this coordination context
    /// </summary>
    public string ContextId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// The conversation this context belongs to
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// The current execution plan
    /// </summary>
    public ExecutionPlan? CurrentPlan { get; set; }

    /// <summary>
    /// When this context was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    // ===== PRIVATE COLLECTIONS (thread-safe access via methods) =====
    private readonly Dictionary<string, SharedFact> _facts = new();
    private readonly List<SubAgentFindingV2> _findings = [];
    private readonly List<PendingQuestion> _questions = [];
    private readonly List<ConflictRecord> _conflicts = [];
    private readonly List<CoordinationEvent> _eventLog = [];

    // ===== FACTS MANAGEMENT =====

    /// <summary>
    /// Add a discovered fact
    /// </summary>
    public void AddFact(SharedFact fact)
    {
        lock (_lock)
        {
            _facts[fact.FactId] = fact;
        }
    }

    /// <summary>
    /// Add multiple facts at once
    /// </summary>
    public void AddFacts(IEnumerable<SharedFact> facts)
    {
        lock (_lock)
        {
            foreach (var fact in facts)
            {
                _facts[fact.FactId] = fact;
            }
        }
    }

    /// <summary>
    /// Get a fact by ID
    /// </summary>
    public SharedFact? GetFact(string factId)
    {
        lock (_lock)
        {
            return _facts.TryGetValue(factId, out var fact) && !fact.IsExpired ? fact : null;
        }
    }

    /// <summary>
    /// Get a fact by key (returns most recent)
    /// </summary>
    public SharedFact? GetFactByKey(string key)
    {
        lock (_lock)
        {
            return _facts
                .Values.Where(f => f.Key == key && !f.IsExpired)
                .OrderByDescending(f => f.DiscoveredAt)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Get all facts in a category
    /// </summary>
    public IReadOnlyList<SharedFact> GetFactsByCategory(string category)
    {
        lock (_lock)
        {
            return _facts
                .Values.Where(f => f.Category == category && !f.IsExpired)
                .OrderByDescending(f => f.DiscoveredAt)
                .ToList();
        }
    }

    /// <summary>
    /// Get all facts from a specific agent
    /// </summary>
    public IReadOnlyList<SharedFact> GetFactsByAgent(string agentName)
    {
        lock (_lock)
        {
            return _facts.Values.Where(f => f.SourceAgent == agentName && !f.IsExpired).ToList();
        }
    }

    /// <summary>
    /// Get all non-expired facts
    /// </summary>
    public IReadOnlyList<SharedFact> GetAllFacts()
    {
        lock (_lock)
        {
            return _facts.Values.Where(f => !f.IsExpired).ToList();
        }
    }

    /// <summary>
    /// Check if a fact key exists
    /// </summary>
    public bool HasFact(string key)
    {
        lock (_lock)
        {
            return _facts.Values.Any(f => f.Key == key && !f.IsExpired);
        }
    }

    /// <summary>
    /// Get a fact value with type conversion and default
    /// </summary>
    public T? GetFactValue<T>(string key, T? defaultValue = default)
    {
        var fact = GetFactByKey(key);
        if (fact == null)
            return defaultValue;
        return fact.GetValueAs(defaultValue);
    }

    // ===== FINDINGS MANAGEMENT =====

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
    /// Get all findings
    /// </summary>
    public IReadOnlyList<SubAgentFindingV2> GetAllFindings()
    {
        lock (_lock)
        {
            return _findings.ToList();
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
    /// Get the most recent finding for an agent
    /// </summary>
    public SubAgentFindingV2? GetLatestFindingForAgent(string agentName)
    {
        lock (_lock)
        {
            return _findings
                .Where(f => f.SubAgentName == agentName)
                .OrderByDescending(f => f.CompletedAt)
                .FirstOrDefault();
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
    /// Get blocking concerns
    /// </summary>
    public IReadOnlyList<Concern> GetBlockingConcerns()
    {
        lock (_lock)
        {
            return _findings.SelectMany(f => f.Concerns).Where(c => c.Blocking).ToList();
        }
    }

    // ===== QUESTIONS MANAGEMENT =====

    /// <summary>
    /// Ask a question to another agent
    /// </summary>
    public PendingQuestion AskQuestion(
        string fromAgent,
        string toAgent,
        string question,
        string? context = null,
        bool blocking = false
    )
    {
        var q = new PendingQuestion
        {
            FromAgent = fromAgent,
            ToAgent = toAgent,
            Question = question,
            Context = context,
            IsBlocking = blocking,
        };

        lock (_lock)
        {
            _questions.Add(q);
        }

        return q;
    }

    /// <summary>
    /// Get unanswered questions for an agent
    /// </summary>
    public IReadOnlyList<PendingQuestion> GetPendingQuestionsFor(string agentName)
    {
        lock (_lock)
        {
            return _questions.Where(q => q.ToAgent == agentName && !q.IsAnswered).ToList();
        }
    }

    /// <summary>
    /// Get answered questions that an agent asked
    /// </summary>
    public IReadOnlyList<PendingQuestion> GetAnsweredQuestionsFrom(string agentName)
    {
        lock (_lock)
        {
            return _questions.Where(q => q.FromAgent == agentName && q.IsAnswered).ToList();
        }
    }

    /// <summary>
    /// Answer a pending question
    /// </summary>
    public void AnswerQuestion(string questionId, string answer)
    {
        lock (_lock)
        {
            var index = _questions.FindIndex(q => q.QuestionId == questionId);
            if (index >= 0)
            {
                _questions[index] = _questions[index] with
                {
                    Answer = answer,
                    AnsweredAt = DateTime.UtcNow,
                };
            }
        }
    }

    /// <summary>
    /// Check if there are blocking unanswered questions
    /// </summary>
    public bool HasBlockingQuestions()
    {
        lock (_lock)
        {
            return _questions.Any(q => q.IsBlocking && !q.IsAnswered);
        }
    }

    // ===== CONFLICTS MANAGEMENT =====

    /// <summary>
    /// Record a conflict
    /// </summary>
    public void RecordConflict(ConflictRecord conflict)
    {
        lock (_lock)
        {
            _conflicts.Add(conflict);
        }
    }

    /// <summary>
    /// Add a conflict (alias for RecordConflict)
    /// </summary>
    public void AddConflict(ConflictRecord conflict) => RecordConflict(conflict);

    /// <summary>
    /// Get all conflicts
    /// </summary>
    public IReadOnlyList<ConflictRecord> GetAllConflicts()
    {
        lock (_lock)
        {
            return _conflicts.ToList();
        }
    }

    /// <summary>
    /// Get unresolved conflicts
    /// </summary>
    public IReadOnlyList<ConflictRecord> GetUnresolvedConflicts()
    {
        lock (_lock)
        {
            return _conflicts.Where(c => !c.IsResolved).ToList();
        }
    }

    /// <summary>
    /// Resolve a conflict
    /// </summary>
    public void ResolveConflict(string conflictId, ConflictResolution resolution)
    {
        lock (_lock)
        {
            var conflict = _conflicts.FirstOrDefault(c => c.ConflictId == conflictId);
            if (conflict != null)
            {
                var index = _conflicts.IndexOf(conflict);
                _conflicts[index] = conflict with { Resolution = resolution };
            }
        }
    }

    // ===== EVENT LOG =====

    /// <summary>
    /// Log a coordination event
    /// </summary>
    public void LogEvent(CoordinationEvent evt)
    {
        lock (_lock)
        {
            _eventLog.Add(evt);
        }
    }

    /// <summary>
    /// Get all logged events
    /// </summary>
    public IReadOnlyList<CoordinationEvent> GetEventLog()
    {
        lock (_lock)
        {
            return _eventLog.ToList();
        }
    }

    // ===== BRIEFING GENERATION =====

    /// <summary>
    /// Generate a coordination briefing for a sub-agent
    /// </summary>
    public SubAgentCoordinationBriefing GenerateBriefing(string forAgent, ExecutionStep step)
    {
        lock (_lock)
        {
            // Get facts relevant to this agent's task
            var relevantFacts = _facts
                .Values.Where(f => !f.IsExpired)
                .Where(f =>
                    step.RequiredFactIds.Contains(f.FactId)
                    || step.RequiredFactCategories.Contains(f.Category)
                    || IsRelevantToAgent(f, forAgent)
                )
                .ToList();

            // Get findings from dependent steps
            var dependentAgents =
                CurrentPlan
                    ?.Steps.Where(s => step.DependsOn.Contains(s.StepId))
                    .Select(s => s.SubAgentName)
                    .ToHashSet() ?? [];

            var previousFindings = _findings
                .Where(f => dependentAgents.Contains(f.SubAgentName))
                .ToList();

            // Get questions this agent needs to answer
            var questionsToAnswer = _questions
                .Where(q => q.ToAgent == forAgent && !q.IsAnswered)
                .ToList();

            // Get answers to questions this agent asked
            var answeredQuestions = _questions
                .Where(q => q.FromAgent == forAgent && q.IsAnswered)
                .ToList();

            // Get active (non-blocking) concerns
            var activeConcerns = _findings
                .SelectMany(f => f.Concerns)
                .Where(c => !c.Blocking)
                .ToList();

            return new SubAgentCoordinationBriefing
            {
                ForAgent = forAgent,
                Step = step,
                RelevantFacts = relevantFacts,
                PreviousFindings = previousFindings,
                QuestionsToAnswer = questionsToAnswer,
                AnswersReceived = answeredQuestions,
                ActiveConcerns = activeConcerns,
            };
        }
    }

    /// <summary>
    /// Determine if a fact is relevant to an agent based on category
    /// </summary>
    private static bool IsRelevantToAgent(SharedFact fact, string agentName)
    {
        // Define which fact categories are relevant to which agents
        var relevance = new Dictionary<string, HashSet<string>>
        {
            ["PortfolioManager"] =
            [
                FactCategories.Portfolio,
                FactCategories.Holdings,
                FactCategories.CustomerData,
                FactCategories.Account,
            ],
            ["InvestmentAdvisor"] =
            [
                FactCategories.Funds,
                FactCategories.Market,
                FactCategories.Risk,
                FactCategories.CustomerData,
            ],
            ["AccountServices"] =
            [
                FactCategories.Account,
                FactCategories.CustomerData,
                FactCategories.Transaction,
            ],
            ["ComplianceOfficer"] =
            [
                FactCategories.Risk,
                FactCategories.Compliance,
                FactCategories.CustomerData,
                FactCategories.Portfolio,
            ],
            ["ProfitProjection"] =
            [
                FactCategories.Portfolio,
                FactCategories.Funds,
                FactCategories.Market,
                FactCategories.Projection,
            ],
            ["ExternalApiServices"] =
            [
                FactCategories.CustomerData,
                FactCategories.ApiResponse,
                FactCategories.Portfolio,
                FactCategories.Funds,
            ],
        };

        return relevance.TryGetValue(agentName, out var categories)
            && categories.Contains(fact.Category);
    }

    // ===== SUMMARY =====

    /// <summary>
    /// Generate a summary of the current coordination state
    /// </summary>
    public string GenerateSummary()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"## Coordination Context: {ContextId}");
            sb.AppendLine($"Conversation: {ConversationId}");
            sb.AppendLine();

            if (_facts.Count > 0)
            {
                sb.AppendLine($"### Facts ({_facts.Count})");
                foreach (var fact in _facts.Values.Take(10))
                {
                    sb.AppendLine($"- {fact.ToDisplayString()} (from {fact.SourceAgent})");
                }
                if (_facts.Count > 10)
                    sb.AppendLine($"  ... and {_facts.Count - 10} more");
                sb.AppendLine();
            }

            if (_findings.Count > 0)
            {
                sb.AppendLine($"### Findings ({_findings.Count})");
                foreach (var finding in _findings)
                {
                    sb.AppendLine(
                        $"- [{finding.SubAgentName}] {finding.Status}: {finding.Summary ?? finding.TaskDescription}"
                    );
                }
                sb.AppendLine();
            }

            var recommendations = GetAllRecommendations();
            if (recommendations.Count > 0)
            {
                sb.AppendLine($"### Recommendations ({recommendations.Count})");
                foreach (var rec in recommendations.Take(5))
                {
                    sb.AppendLine($"- {rec.Description} ({rec.Confidence:P0})");
                }
                sb.AppendLine();
            }

            var concerns = GetAllConcerns();
            if (concerns.Count > 0)
            {
                sb.AppendLine($"### Concerns ({concerns.Count})");
                foreach (var concern in concerns)
                {
                    var blocking = concern.Blocking ? " [BLOCKING]" : "";
                    sb.AppendLine($"- [{concern.Severity}]{blocking} {concern.Description}");
                }
            }

            return sb.ToString();
        }
    }
}

/// <summary>
/// Briefing provided to a sub-agent before execution
/// </summary>
public record SubAgentCoordinationBriefing
{
    /// <summary>
    /// Which agent this briefing is for
    /// </summary>
    public required string ForAgent { get; init; }

    /// <summary>
    /// The execution step
    /// </summary>
    public required ExecutionStep Step { get; init; }

    /// <summary>
    /// Facts relevant to this agent's task
    /// </summary>
    public List<SharedFact> RelevantFacts { get; init; } = [];

    /// <summary>
    /// Findings from agents that ran before (dependencies)
    /// </summary>
    public List<SubAgentFindingV2> PreviousFindings { get; init; } = [];

    /// <summary>
    /// Questions this agent needs to answer
    /// </summary>
    public List<PendingQuestion> QuestionsToAnswer { get; init; } = [];

    /// <summary>
    /// Answers to questions this agent previously asked
    /// </summary>
    public List<PendingQuestion> AnswersReceived { get; init; } = [];

    /// <summary>
    /// Active concerns from other agents
    /// </summary>
    public List<Concern> ActiveConcerns { get; init; } = [];

    /// <summary>
    /// Check if this briefing has useful context
    /// </summary>
    public bool HasContext =>
        RelevantFacts.Count > 0
        || PreviousFindings.Count > 0
        || QuestionsToAnswer.Count > 0
        || AnswersReceived.Count > 0;

    /// <summary>
    /// Convert to a prompt-friendly string
    /// </summary>
    public string ToPromptContext()
    {
        if (!HasContext)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("=== COORDINATION CONTEXT ===");
        sb.AppendLine();

        if (RelevantFacts.Count > 0)
        {
            sb.AppendLine("## Known Facts (verified data you can reference)");
            foreach (var fact in RelevantFacts)
            {
                sb.AppendLine(
                    $"- **{fact.Key}**: {fact.Value} (from {fact.SourceAgent}, {fact.Confidence:P0} confidence)"
                );
            }
            sb.AppendLine();
        }

        if (PreviousFindings.Count > 0)
        {
            sb.AppendLine("## Previous Agent Work");
            foreach (var finding in PreviousFindings)
            {
                sb.AppendLine($"### {finding.SubAgentName} ({finding.Status})");
                if (!string.IsNullOrEmpty(finding.Summary))
                {
                    sb.AppendLine($"Summary: {finding.Summary}");
                }
                if (finding.Recommendations.Count > 0)
                {
                    sb.AppendLine("Recommendations:");
                    foreach (var rec in finding.Recommendations)
                    {
                        sb.AppendLine($"  - {rec.Description} ({rec.Confidence:P0} confidence)");
                    }
                }
                sb.AppendLine();
            }
        }

        if (QuestionsToAnswer.Count > 0)
        {
            sb.AppendLine("## Questions You Need to Answer");
            foreach (var q in QuestionsToAnswer)
            {
                sb.AppendLine($"- From {q.FromAgent}: {q.Question}");
                if (!string.IsNullOrEmpty(q.Context))
                {
                    sb.AppendLine($"  Context: {q.Context}");
                }
            }
            sb.AppendLine();
        }

        if (AnswersReceived.Count > 0)
        {
            sb.AppendLine("## Answers to Your Previous Questions");
            foreach (var q in AnswersReceived)
            {
                sb.AppendLine($"- Q: {q.Question}");
                sb.AppendLine($"  A ({q.ToAgent}): {q.Answer}");
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
            sb.AppendLine();
        }

        sb.AppendLine("=== END COORDINATION CONTEXT ===");
        sb.AppendLine();

        return sb.ToString();
    }
}
