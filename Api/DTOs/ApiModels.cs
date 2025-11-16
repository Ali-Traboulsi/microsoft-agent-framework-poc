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
