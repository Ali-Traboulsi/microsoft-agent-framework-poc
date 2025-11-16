using AgentFrameworkQuickStart.Api.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace AgentFrameworkQuickStart.Api.Hubs;

public class AgentHub : Hub
{
    private readonly AgentService _agentService;

    public AgentHub(AgentService agentService)
    {
        _agentService = agentService;
    }

    public async IAsyncEnumerable<StreamingChatResponse> ChatStream(
        string message,
        string agentName
    )
    {
        var agent = _agentService.GetAgentByName(agentName);

        await foreach (var update in agent.RunStreamingAsync(message))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return new StreamingChatResponse(update.Text, agentName, false);
            }
        }

        // Send completion signal
        yield return new StreamingChatResponse("", agentName, true);
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
