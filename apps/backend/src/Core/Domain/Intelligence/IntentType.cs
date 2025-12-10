namespace AgentFrameworkQuickStart.Core.Domain.Intelligence;

/// <summary>
/// Categorizes the type of user intent for intelligent routing
/// </summary>
public enum IntentType
{
    // Investment & Projection Intents
    ProfitProjection,
    InvestmentAdvice,
    FundRecommendation,
    RiskAssessment,

    // Portfolio Intents
    PortfolioCreation,
    PortfolioAnalysis,
    PortfolioRebalancing,
    HoldingsInquiry,

    // Account Intents
    AccountBalance,
    AccountCreation,
    FundTransfer,
    TransactionHistory,

    // External API Intents
    MutualFundSearch,
    MutualFundDetails,
    FundInOperation,
    SNBCapitalQuery,
    CustomerDataLookup, // CIF-based customer data lookups

    // Compliance Intents
    ComplianceCheck,
    RiskVerification,
    RegulatoryInquiry,

    // Information Intents
    GeneralInquiry,
    WebSearch,
    MarketNews,

    // Conversational Intents
    Greeting,
    Clarification,
    Confirmation,
    Cancellation,

    // Composite Intents (require multiple agents)
    CompleteInvestmentWorkflow,
    PortfolioWithProjection,
    ComprehensiveAnalysis,

    // Unknown
    Unknown,
}

/// <summary>
/// Urgency level of the user request
/// </summary>
public enum UrgencyLevel
{
    Low, // General inquiry, no time pressure
    Medium, // Standard request, reasonable response time
    High, // User indicates urgency or time-sensitive matter
    Critical, // Transaction in progress, immediate action needed
}

/// <summary>
/// Execution strategy for handling the intent
/// </summary>
public enum ExecutionStrategy
{
    /// <summary>Single sub-agent can handle this</summary>
    SingleAgent,

    /// <summary>Multiple agents needed, run in sequence</summary>
    Sequential,

    /// <summary>Multiple agents can run in parallel</summary>
    Parallel,

    /// <summary>Master agent handles directly (greeting, clarification)</summary>
    DirectResponse,

    /// <summary>Need more information before proceeding</summary>
    NeedsClarification,

    /// <summary>Complex workflow with dependencies</summary>
    Workflow,

    /// <summary>Master coordinates with review of sub-agent work</summary>
    Hierarchical,
}
