using ProjectionMessages = AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;

namespace AgentFrameworkQuickStart.Api.DTOs;

public record ChatRequest(string Message, string AgentName);

public record ChatResponse(string Message, string AgentName, DateTime Timestamp);

public record StreamingChatResponse(string Message, string AgentName, bool IsComplete);

public record PortfolioRequest(string AccountId, string PortfolioName, string Strategy);

public record InvestmentRequest(
    string AccountId,
    string PortfolioId,
    string FundSymbol,
    decimal Amount
);

public record FundSearchRequest(
    string? Category = null,
    string? RiskLevel = null,
    decimal? MinReturn = null
);

public record WorkflowRequest(
    string Input,
    string WorkflowType // "sequential" or "concurrent"
);

public record ApiResponse<T>(bool Success, T? Data, string? Error);

/// <summary>
/// Represents different types of content that can be sent to the agent
/// </summary>
public record ContentInput
{
    /// <summary>
    /// Type of content: "text", "image", "audio", "uri"
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Text content (for Type = "text")
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Base64-encoded data (for Type = "image" or "audio")
    /// </summary>
    public string? Data { get; set; }

    /// <summary>
    /// URI reference (for Type = "uri")
    /// </summary>
    public string? Uri { get; set; }

    /// <summary>
    /// Media type (e.g., "image/png", "audio/wav", "application/pdf")
    /// </summary>
    public string? MediaType { get; set; }

    /// <summary>
    /// Optional filename for uploaded files
    /// </summary>
    public string? FileName { get; set; }
}

/// <summary>
/// Request for multi-modal chat with text, images, audio, files
/// </summary>
public record MultiModalChatRequest
{
    /// <summary>
    /// Optional text message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// List of content items (text, images, audio, URIs)
    /// </summary>
    public List<ContentInput> Contents { get; set; } = new();

    /// <summary>
    /// Optional conversation ID for maintaining context
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// Enable extended reasoning mode (like ChatGPT o1)
    /// </summary>
    public bool EnableThinking { get; set; } = false;
}

/// <summary>
/// Response for multi-modal chat
/// </summary>
public record MultiModalChatResponse
{
    public required string Message { get; set; }
    public DateTime Timestamp { get; set; }
    public long ProcessingTimeMs { get; set; }
    public List<string> ContentTypesProcessed { get; set; } = new();
    public string? ConversationId { get; set; }
}

/// <summary>
/// Request for multi-modal chat using form-data file uploads
/// </summary>
public class MultiModalFormRequest
{
    /// <summary>
    /// Optional text message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Uploaded files (images, audio, PDFs, etc.)
    /// </summary>
    public List<IFormFile>? Files { get; set; }

    /// <summary>
    /// Optional URIs to include (comma-separated or multiple fields)
    /// </summary>
    public string? Uris { get; set; }

    /// <summary>
    /// Optional conversation ID for maintaining context
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// Enable extended reasoning mode (like ChatGPT o1)
    /// </summary>
    public bool EnableThinking { get; set; } = false;
}

#region Projection DTOs

/// <summary>
/// Request for structured profit projection calculation
/// </summary>
public class ProjectionChatRequest
{
    /// <summary>Investment amount in the specified currency</summary>
    public decimal InvestmentAmount { get; set; }

    /// <summary>Currency code (SAR, USD, EUR). Default: SAR</summary>
    public string? Currency { get; set; } = "SAR";

    /// <summary>Investment time horizon in months</summary>
    public int TimeHorizonMonths { get; set; }

    /// <summary>Risk profile: Conservative, Moderate, or Aggressive</summary>
    public string? RiskProfile { get; set; } = "Moderate";

    /// <summary>Investment type: LumpSum or Monthly</summary>
    public string? InvestmentType { get; set; } = "LumpSum";

    /// <summary>Monthly investment amount (for Monthly/SIP type)</summary>
    public decimal? MonthlyAmount { get; set; }

    /// <summary>Customer ID (CIF) for personalized projections</summary>
    public string? CustomerId { get; set; }

    /// <summary>Only include Shariah-compliant funds</summary>
    public bool? ShariahCompliantOnly { get; set; } = false;

    /// <summary>Optional conversation ID for context</summary>
    public string? ConversationId { get; set; }
}

/// <summary>
/// Structured projection response with full projection data
/// </summary>
public class ProjectionResponseDto
{
    public bool Success { get; set; }
    public string ProjectionId { get; set; } = "";
    public ProjectionMessages.ProjectionSummary InputSummary { get; set; } = null!;
    public ProjectionMessages.ScenarioSet Scenarios { get; set; } = null!;
    public List<ProjectionMessages.FundRecommendation> RecommendedFunds { get; set; } = new();
    public List<string> RiskWarnings { get; set; } = new();
    public ProjectionMessages.CallToAction CallToAction { get; set; } = null!;
    public ProjectionMessages.ProjectionMetadata Metadata { get; set; } = null!;
    public long ProcessingTimeMs { get; set; }
    public string ConversationId { get; set; } = "";
}

/// <summary>
/// Response for natural language projection chat
/// </summary>
public class ProjectionChatResponseDto
{
    public bool Success { get; set; }
    public string Response { get; set; } = "";
    public List<string> SubAgentsUsed { get; set; } = new();
    public long ProcessingTimeMs { get; set; }
    public string ConversationId { get; set; } = "";
    public string? Hint { get; set; }
}

#endregion
