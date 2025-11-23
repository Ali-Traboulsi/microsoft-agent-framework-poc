namespace AgentFrameworkQuickStart.Api.DTOs;

/// <summary>
/// Structured response for thinking mode - separates reasoning from final answer
/// </summary>
public class ThinkingModeResponse
{
    /// <summary>
    /// The AI's reasoning process and analysis
    /// </summary>
    public string Thinking { get; set; } = string.Empty;

    /// <summary>
    /// The final answer or results
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Any reference links used in the thinking process
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
