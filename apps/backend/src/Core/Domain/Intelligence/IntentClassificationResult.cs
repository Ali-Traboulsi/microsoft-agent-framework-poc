using System.Text.Json.Serialization;

namespace AgentFrameworkQuickStart.Core.Domain.Intelligence;

/// <summary>
/// LLM-structured output for intent classification
/// This schema is used to get structured JSON from the LLM
/// </summary>
public class IntentClassificationResult
{
    /// <summary>
    /// Primary intent as a string (maps to IntentType enum)
    /// </summary>
    [JsonPropertyName("primary_intent")]
    public string PrimaryIntent { get; set; } = "Unknown";

    /// <summary>
    /// Secondary intents for composite requests
    /// </summary>
    [JsonPropertyName("secondary_intents")]
    public List<string> SecondaryIntents { get; set; } = [];

    /// <summary>
    /// Confidence score (0.0 - 1.0)
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Extracted entities
    /// </summary>
    [JsonPropertyName("entities")]
    public List<EntityExtractionResult> Entities { get; set; } = [];

    /// <summary>
    /// Which sub-agents are needed
    /// </summary>
    [JsonPropertyName("required_sub_agents")]
    public List<string> RequiredSubAgents { get; set; } = [];

    /// <summary>
    /// Recommended execution strategy
    /// </summary>
    [JsonPropertyName("execution_strategy")]
    public string ExecutionStrategy { get; set; } = "SingleAgent";

    /// <summary>
    /// Urgency level
    /// </summary>
    [JsonPropertyName("urgency")]
    public string Urgency { get; set; } = "Medium";

    /// <summary>
    /// Detected language (en, ar, etc.)
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    /// <summary>
    /// Inferred user expertise level
    /// </summary>
    [JsonPropertyName("user_expertise")]
    public string UserExpertise { get; set; } = "intermediate";

    /// <summary>
    /// Sentiment analysis
    /// </summary>
    [JsonPropertyName("sentiment")]
    public string Sentiment { get; set; } = "neutral";

    /// <summary>
    /// Brief summary of what the user wants
    /// </summary>
    [JsonPropertyName("intent_summary")]
    public string IntentSummary { get; set; } = string.Empty;

    /// <summary>
    /// Refined request to pass to sub-agent
    /// </summary>
    [JsonPropertyName("refined_request")]
    public string RefinedRequest { get; set; } = string.Empty;

    /// <summary>
    /// Any ambiguities detected
    /// </summary>
    [JsonPropertyName("ambiguities")]
    public List<string> Ambiguities { get; set; } = [];

    /// <summary>
    /// Suggested clarifying questions
    /// </summary>
    [JsonPropertyName("suggested_clarifications")]
    public List<string> SuggestedClarifications { get; set; } = [];

    /// <summary>
    /// Default values to apply for missing information
    /// </summary>
    [JsonPropertyName("applied_defaults")]
    public Dictionary<string, object> AppliedDefaults { get; set; } = new();
}

/// <summary>
/// Entity extraction result from LLM
/// </summary>
public class EntityExtractionResult
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("raw_value")]
    public string RawValue { get; set; } = string.Empty;

    [JsonPropertyName("normalized_value")]
    public object? NormalizedValue { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 1.0;
}
