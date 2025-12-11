using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Middleware;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator - Sub-agent delegation methods
/// </summary>
public partial class MasterOrchestratorHelper
{
    [Description("Delegate a request to a specific sub-agent specialist")]
    public async Task<string> DelegateToSubAgent(
        [Description(
            "The name of the sub-agent: PortfolioManager, InvestmentAdvisor, AccountServices, ComplianceOfficer, ProfitProjection, or ExternalApiServices (for SNB Capital API, mutual funds, portfolios, and fund-in operations)"
        )]
            string subAgentName,
        [Description("The specific task or question for the sub-agent")] string request,
        [Description("Optional context as JSON string")] string? context = null
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.DelegateToSubAgent",
            ActivityKind.Internal
        );
        activity?.SetTag("subagent.name", subAgentName);
        activity?.SetTag("request.length", request.Length);

        _logger.LogInformation(
            "Master Agent delegating to {SubAgent}: {Request}",
            subAgentName,
            request
        );

        if (!_subAgentLookup.TryGetValue(subAgentName, out var subAgent))
        {
            var availableAgents = string.Join(", ", _subAgentLookup.Keys);
            var errorMsg =
                $"❌ **Error:** Sub-agent '{subAgentName}' not found.\n\n"
                + $"**Available sub-agents:** {availableAgents}";

            _logger.LogWarning(
                "Invalid sub-agent requested: {SubAgent}. Available: {Available}",
                subAgentName,
                availableAgents
            );

            activity?.SetStatus(ActivityStatusCode.Error, "Sub-agent not found");
            DelegationErrorCounter.Add(1, new KeyValuePair<string, object?>("error", "not_found"));

            return errorMsg;
        }

        var contextDict = string.IsNullOrEmpty(context)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(context);

        // Get conversation ID for sub-agent thread management
        var conversationId =
            DelegationEventMiddleware.CurrentConversationId ?? Guid.NewGuid().ToString();

        var sw = Stopwatch.StartNew();
        var response = await subAgent.HandleRequestAsync(request, conversationId, contextDict);
        sw.Stop();

        // Record metrics
        DelegationCounter.Add(
            1,
            new KeyValuePair<string, object?>("subagent", subAgentName),
            new KeyValuePair<string, object?>("success", response.Success)
        );
        DelegationDuration.Record(
            response.DurationMs,
            new KeyValuePair<string, object?>("subagent", subAgentName)
        );

        if (response.Success)
        {
            var result = new StringBuilder();
            result.AppendLine($"✅ **{subAgent.Name} Response:**\n");
            result.AppendLine(response.Result);
            result.AppendLine();
            result.AppendLine($"📊 *Tools used:* {string.Join(", ", response.ToolsUsed)}");
            result.AppendLine($"⏱️ *Duration:* {response.DurationMs}ms");

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.tools_used", response.ToolsUsed.Count);
            activity?.SetTag("response.duration_ms", response.DurationMs);

            _logger.LogInformation(
                "{SubAgent} delegation successful in {Duration}ms",
                subAgentName,
                response.DurationMs
            );

            return result.ToString();
        }
        else
        {
            var errorResult = $"❌ **{subAgent.Name} Error:**\n{response.ErrorMessage}";

            activity?.SetStatus(ActivityStatusCode.Error, response.ErrorMessage);
            DelegationErrorCounter.Add(
                1,
                new KeyValuePair<string, object?>("subagent", subAgentName),
                new KeyValuePair<string, object?>("error", "execution_failed")
            );

            _logger.LogError(
                "{SubAgent} delegation failed: {Error}",
                subAgentName,
                response.ErrorMessage
            );

            return errorResult;
        }
    }

    [Description("Delegate to multiple sub-agents for complex multi-domain requests")]
    public async Task<string> DelegateToMultipleSubAgents(
        [Description(
            "JSON array of delegation requests: [{\"subAgent\": \"name\", \"request\": \"task\", \"context\": \"optional\"}]"
        )]
            string delegationsJson
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.DelegateToMultipleSubAgents",
            ActivityKind.Internal
        );

        _logger.LogInformation("Master Agent delegating to multiple sub-agents");

        List<DelegationRequest> delegations;
        try
        {
            delegations =
                JsonSerializer.Deserialize<List<DelegationRequest>>(delegationsJson)
                ?? throw new JsonException("Failed to parse delegations");

            activity?.SetTag("delegations.count", delegations.Count);
            activity?.SetTag(
                "delegations.subagents",
                string.Join(",", delegations.Select(d => d.SubAgent))
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse delegation JSON: {Json}", delegationsJson);
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid JSON format");
            DelegationErrorCounter.Add(
                1,
                new KeyValuePair<string, object?>("error", "invalid_json")
            );

            return $"❌ **Error:** Invalid delegation format. {ex.Message}";
        }

        var results = new StringBuilder();
        results.AppendLine("## Multi-Agent Coordination Results\n");

        var totalSw = Stopwatch.StartNew();

        foreach (var delegation in delegations)
        {
            results.AppendLine($"### {delegation.SubAgent}\n");

            var result = await DelegateToSubAgent(
                delegation.SubAgent,
                delegation.Request,
                delegation.Context
            );

            results.AppendLine(result);
            results.AppendLine("\n---\n");
        }

        totalSw.Stop();
        results.AppendLine($"**Total coordination time:** {totalSw.ElapsedMilliseconds}ms");

        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("total.duration_ms", totalSw.ElapsedMilliseconds);

        // Record multi-agent coordination metric
        DelegationDuration.Record(
            totalSw.ElapsedMilliseconds,
            new KeyValuePair<string, object?>("delegation_type", "multiple"),
            new KeyValuePair<string, object?>("count", delegations.Count)
        );

        _logger.LogInformation(
            "Multi-agent delegation completed in {Duration}ms for {Count} agents",
            totalSw.ElapsedMilliseconds,
            delegations.Count
        );

        return results.ToString();
    }
}

/// <summary>
/// Delegation request for multiple sub-agents
/// </summary>
internal class DelegationRequest
{
    public required string SubAgent { get; set; }
    public required string Request { get; set; }
    public string? Context { get; set; }
}
