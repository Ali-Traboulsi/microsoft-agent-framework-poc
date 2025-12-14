namespace AgentFrameworkQuickStart.Core.Domain.Memory;

/// <summary>
/// Represents long-term user memory that persists across conversations.
/// Stores user preferences, frequently used entities, learned patterns, and expertise indicators.
/// </summary>
public class UserMemory
{
    /// <summary>
    /// Unique identifier for this user memory record
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User's preferred language (learned from interactions)
    /// </summary>
    public string PreferredLanguage { get; set; } = "en";

    /// <summary>
    /// Detected expertise level based on historical interactions
    /// </summary>
    public ExpertiseLevel ExpertiseLevel { get; set; } = ExpertiseLevel.Intermediate;

    /// <summary>
    /// Inferred risk tolerance from past investment discussions
    /// </summary>
    public RiskTolerance RiskTolerance { get; set; } = RiskTolerance.Moderate;

    /// <summary>
    /// Frequently used account IDs (most recent first)
    /// </summary>
    public List<FrequentEntity> FrequentAccounts { get; set; } = [];

    /// <summary>
    /// Frequently mentioned portfolio IDs
    /// </summary>
    public List<FrequentEntity> FrequentPortfolios { get; set; } = [];

    /// <summary>
    /// Frequently referenced funds
    /// </summary>
    public List<FrequentEntity> FrequentFunds { get; set; } = [];

    /// <summary>
    /// User preferences learned from conversations
    /// </summary>
    public Dictionary<string, UserPreference> Preferences { get; set; } = new();

    /// <summary>
    /// Key facts remembered about the user
    /// </summary>
    public List<MemorizedFact> Facts { get; set; } = [];

    /// <summary>
    /// Investment interests and goals mentioned historically
    /// </summary>
    public List<InvestmentInterest> Interests { get; set; } = [];

    /// <summary>
    /// Topics/domains the user frequently asks about
    /// </summary>
    public Dictionary<string, int> TopicFrequency { get; set; } = new();

    /// <summary>
    /// Communication style preferences
    /// </summary>
    public CommunicationPreferences CommunicationStyle { get; set; } = new();

    /// <summary>
    /// When this memory was first created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this memory was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of conversations this user has had
    /// </summary>
    public int TotalConversations { get; set; }

    /// <summary>
    /// Total number of turns across all conversations
    /// </summary>
    public int TotalTurns { get; set; }
}

/// <summary>
/// Represents a frequently used entity (account, portfolio, fund)
/// </summary>
public class FrequentEntity
{
    public required string EntityId { get; set; }
    public string? DisplayName { get; set; }
    public int UsageCount { get; set; }
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// A learned user preference
/// </summary>
public class UserPreference
{
    public required string PreferenceKey { get; set; }
    public required string Value { get; set; }
    public double Confidence { get; set; } = 0.5;
    public string? LearnedFrom { get; set; } // ConversationId where this was learned
    public DateTime LearnedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A fact memorized about the user
/// </summary>
public class MemorizedFact
{
    public string FactId { get; set; } = Guid.NewGuid().ToString();
    public required string Category { get; set; } // e.g., "investment_goal", "constraint", "personal"
    public required string Fact { get; set; }
    public double Importance { get; set; } = 0.5; // 0-1 scale
    public double Confidence { get; set; } = 0.5;
    public string? SourceConversationId { get; set; }
    public DateTime LearnedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; } // Some facts may be time-sensitive
}

/// <summary>
/// Investment interests mentioned by the user
/// </summary>
public class InvestmentInterest
{
    public required string Topic { get; set; } // e.g., "technology stocks", "dividend funds"
    public int MentionCount { get; set; }
    public DateTime FirstMentioned { get; set; } = DateTime.UtcNow;
    public DateTime LastMentioned { get; set; } = DateTime.UtcNow;
    public string? SentimentIndicator { get; set; } // "positive", "negative", "curious"
}

/// <summary>
/// Communication style preferences
/// </summary>
public class CommunicationPreferences
{
    /// <summary>
    /// Prefers detailed explanations vs concise answers
    /// </summary>
    public DetailLevel PreferredDetailLevel { get; set; } = DetailLevel.Balanced;

    /// <summary>
    /// Prefers technical terms vs simple language
    /// </summary>
    public bool PrefersTechnicalLanguage { get; set; } = false;

    /// <summary>
    /// Prefers data/charts vs narrative explanations
    /// </summary>
    public bool PrefersDataVisualization { get; set; } = true;

    /// <summary>
    /// Typical response length preference
    /// </summary>
    public ResponseLengthPreference ResponseLength { get; set; } = ResponseLengthPreference.Medium;
}

public enum ExpertiseLevel
{
    Beginner,
    Intermediate,
    Advanced,
    Expert,
}

public enum RiskTolerance
{
    Conservative,
    Moderate,
    Aggressive,
    VeryAggressive,
}

public enum DetailLevel
{
    Concise,
    Balanced,
    Detailed,
}

public enum ResponseLengthPreference
{
    Short,
    Medium,
    Long,
}
