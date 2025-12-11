using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AgentFrameworkQuickStart.Api.Orchestration;
using AgentFrameworkQuickStart.Core.Domain.Coordination;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;
using AgentFrameworkQuickStart.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AgentFrameworkQuickStart.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time coordination protocol streaming.
/// Clients can watch sub-agents coordinate in real-time.
/// </summary>
public class CoordinationHub(
    CoordinationOrchestrator coordinationOrchestrator,
    IIntentClassifier intentClassifier,
    ILogger<CoordinationHub> logger
) : Hub
{
    /// <summary>
    /// Stream coordination events for a user request.
    /// Clients receive:
    /// - PlanCreated: Initial execution plan
    /// - StepStarted/Completed: Agent progress
    /// - FactDiscovered: New facts learned
    /// - ConflictDetected/Resolved: Conflict resolution
    /// - SynthesisCompleted: Final unified response
    /// </summary>
    public async IAsyncEnumerable<CoordinationStreamMessage> StreamCoordinatedChat(
        string message,
        string? conversationId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        conversationId ??= Guid.NewGuid().ToString("N")[..12];

        logger.LogInformation(
            "Starting coordinated chat stream for {ConversationId}: {Message}",
            conversationId,
            message.Length > 50 ? message[..50] + "..." : message
        );

        // Classify intent first
        var intent = await intentClassifier.ClassifyAsync(message, null, cancellationToken);

        yield return new CoordinationStreamMessage
        {
            Type = "intent_classified",
            Content = $"Intent: {intent.PrimaryIntent} ({intent.Confidence:P0} confidence)",
            Metadata = new Dictionary<string, object>
            {
                ["intent"] = intent.PrimaryIntent.ToString(),
                ["confidence"] = intent.Confidence,
                ["required_agents"] = intent.RequiredSubAgents,
            },
        };

        // Stream coordination events
        await foreach (
            var evt in coordinationOrchestrator.ExecuteCoordinatedAsync(
                message,
                conversationId,
                intent,
                cancellationToken
            )
        )
        {
            yield return MapToStreamMessage(evt);
        }
    }

    /// <summary>
    /// Stream coordination using channel-based approach (for older SignalR clients)
    /// </summary>
    public ChannelReader<CoordinationStreamMessage> StreamCoordinatedChatChannel(
        string message,
        string? conversationId = null
    )
    {
        var channel = Channel.CreateUnbounded<CoordinationStreamMessage>();

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in StreamCoordinatedChat(message, conversationId))
                {
                    await channel.Writer.WriteAsync(msg);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in coordination stream");
                await channel.Writer.WriteAsync(
                    new CoordinationStreamMessage { Type = "error", Content = ex.Message }
                );
            }
            finally
            {
                channel.Writer.Complete();
            }
        });

        return channel.Reader;
    }

    /// <summary>
    /// Get available agents and their capabilities
    /// </summary>
    public Task<AgentCapabilitiesInfo> GetAgentCapabilities()
    {
        // This could be expanded to return actual agent capabilities
        return Task.FromResult(
            new AgentCapabilitiesInfo
            {
                AvailableAgents =
                [
                    new AgentInfo
                    {
                        Name = "PortfolioManager",
                        Domain = "Portfolio operations",
                        Capabilities = ["holdings", "allocation", "creation"],
                    },
                    new AgentInfo
                    {
                        Name = "InvestmentAdvisor",
                        Domain = "Investment advice",
                        Capabilities = ["recommendations", "analysis", "risk"],
                    },
                    new AgentInfo
                    {
                        Name = "AccountServices",
                        Domain = "Account operations",
                        Capabilities = ["balance", "transactions", "lookup"],
                    },
                    new AgentInfo
                    {
                        Name = "ComplianceOfficer",
                        Domain = "Compliance",
                        Capabilities = ["verification", "risk assessment", "limits"],
                    },
                    new AgentInfo
                    {
                        Name = "ProfitProjection",
                        Domain = "Projections",
                        Capabilities = ["profit forecast", "scenarios", "monte carlo"],
                    },
                ],
                ProtocolVersion = "2.0.0",
            }
        );
    }

    private static CoordinationStreamMessage MapToStreamMessage(CoordinationEvent evt)
    {
        return new CoordinationStreamMessage
        {
            Type = evt.Type.ToString().ToLowerInvariant(),
            Content = evt.Content,
            StepNumber = evt.StepNumber,
            TotalSteps = evt.TotalSteps,
            SubAgentName = evt.SubAgentName,
            DurationMs = evt.DurationMs,
            Emoji = evt.GetEmoji(),
            Metadata = evt.Metadata ?? new(),
            Timestamp = evt.Timestamp,
        };
    }
}

/// <summary>
/// Message sent to clients during coordination streaming
/// </summary>
public record CoordinationStreamMessage
{
    /// <summary>
    /// Event type (plan_created, step_started, step_completed, fact_discovered, etc.)
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Human-readable content
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Current step number (1-based)
    /// </summary>
    public int? StepNumber { get; init; }

    /// <summary>
    /// Total number of steps
    /// </summary>
    public int? TotalSteps { get; init; }

    /// <summary>
    /// Agent name if applicable
    /// </summary>
    public string? SubAgentName { get; init; }

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public long? DurationMs { get; init; }

    /// <summary>
    /// Emoji for display
    /// </summary>
    public string? Emoji { get; init; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// When this event occurred
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Information about available agents
/// </summary>
public record AgentCapabilitiesInfo
{
    public List<AgentInfo> AvailableAgents { get; init; } = [];
    public string ProtocolVersion { get; init; } = "2.0.0";
}

/// <summary>
/// Information about a single agent
/// </summary>
public record AgentInfo
{
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public List<string> Capabilities { get; init; } = [];
}
