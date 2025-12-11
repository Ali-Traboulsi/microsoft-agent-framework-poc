namespace AgentFrameworkQuickStart.Core.Domain.Coordination;

/// <summary>
/// Represents a verified fact that sub-agents can reference.
/// Facts are the currency of inter-agent communication.
/// </summary>
public record SharedFact
{
    /// <summary>
    /// Unique identifier for this fact
    /// </summary>
    public string FactId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Category of the fact for filtering (e.g., "customer_data", "portfolio", "compliance", "market")
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// The key/name of the fact (e.g., "risk_profile", "account_balance", "max_equity_exposure")
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The actual value - can be any type (string, number, object, list)
    /// </summary>
    public required object Value { get; init; }

    /// <summary>
    /// Which sub-agent discovered this fact
    /// </summary>
    public required string SourceAgent { get; init; }

    /// <summary>
    /// Which tool was used to discover this fact (for traceability)
    /// </summary>
    public string? SourceTool { get; init; }

    /// <summary>
    /// Confidence in this fact (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// When this fact was discovered
    /// </summary>
    public DateTime DiscoveredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// How long this fact is valid (null = never expires)
    /// </summary>
    public TimeSpan? TimeToLive { get; init; }

    /// <summary>
    /// Check if this fact has expired
    /// </summary>
    public bool IsExpired => TimeToLive.HasValue && DateTime.UtcNow - DiscoveredAt > TimeToLive;

    /// <summary>
    /// Get the value as a specific type with default fallback
    /// </summary>
    public T GetValueAs<T>(T defaultValue = default!)
    {
        try
        {
            if (Value is T typedValue)
                return typedValue;

            // Try conversion for common types
            if (typeof(T) == typeof(string))
                return (T)(object)Value.ToString()!;

            if (typeof(T) == typeof(decimal) && Value is IConvertible)
                return (T)(object)Convert.ToDecimal(Value);

            if (typeof(T) == typeof(int) && Value is IConvertible)
                return (T)(object)Convert.ToInt32(Value);

            if (typeof(T) == typeof(double) && Value is IConvertible)
                return (T)(object)Convert.ToDouble(Value);

            if (typeof(T) == typeof(bool) && Value is IConvertible)
                return (T)(object)Convert.ToBoolean(Value);

            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Create a display-friendly string for streaming to frontend
    /// </summary>
    public string ToDisplayString() => $"{Key}: {Value}";

    /// <summary>
    /// Create a citation string for responses
    /// </summary>
    public string ToCitation() => $"[{SourceAgent}: {Key}={Value}]";
}

/// <summary>
/// Common fact categories for consistent categorization
/// </summary>
public static class FactCategories
{
    public const string CustomerData = "customer_data";
    public const string Account = "account";
    public const string Portfolio = "portfolio";
    public const string Holdings = "holdings";
    public const string Compliance = "compliance";
    public const string Risk = "risk";
    public const string Market = "market";
    public const string Funds = "funds";
    public const string Projection = "projection";
    public const string Transaction = "transaction";
    public const string ApiResponse = "api_response";
}
