using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace AgentFrameworkQuickStart.Api.Hubs;

/// <summary>
/// SignalR hub for master orchestrator with streaming support
/// </summary>
public class MasterAgentHub : Hub
{
    private readonly IMasterOrchestrator _orchestrator;
    private readonly ILogger<MasterAgentHub> _logger;

    public MasterAgentHub(IMasterOrchestrator orchestrator, ILogger<MasterAgentHub> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Master agent client connected: {ConnectionId}",
            Context.ConnectionId
        );
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "Master agent client disconnected: {ConnectionId}, Exception: {Exception}",
            Context.ConnectionId,
            exception?.Message
        );
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Stream chat responses from the master orchestrator
    /// </summary>
    public async IAsyncEnumerable<MasterStreamingResponse> ChatStream(
        string message,
        string conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "Master agent streaming chat for {ConnectionId}, Conversation: {ConversationId}",
            Context.ConnectionId,
            conversationId
        );

        await foreach (
            var response in _orchestrator
                .ProcessRequestStreamingAsync(message, conversationId)
                .WithCancellation(cancellationToken)
        )
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Master agent streaming cancelled for {ConnectionId}",
                    Context.ConnectionId
                );
                break;
            }

            yield return new MasterStreamingResponse
            {
                Type = response.Type.ToString(),
                Content = response.Content,
                SubAgentName = response.SubAgentName,
                ToolName = response.ToolName,
                IsComplete = response.Type == ResponseType.Complete,
                Metadata = response.Metadata,
            };
        }

        _logger.LogInformation(
            "Master agent streaming completed for {ConnectionId}",
            Context.ConnectionId
        );
    }
}

/// <summary>
/// Streaming response from master agent
/// </summary>
public class MasterStreamingResponse
{
    public required string Type { get; set; }
    public string? Content { get; set; }
    public string? SubAgentName { get; set; }
    public string? ToolName { get; set; }
    public bool IsComplete { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
