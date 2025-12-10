using System.Text;
using AgentFrameworkQuickStart.Api.Abstractions;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator - Helper methods and utilities
/// </summary>
public partial class MasterOrchestrator
{
    [System.ComponentModel.Description(
        "Get information about all available sub-agents and their capabilities"
    )]
    public string GetAvailableSubAgentsAsString()
    {
        var result = new StringBuilder();
        result.AppendLine("## Available Sub-Agents\n");

        foreach (var agent in _subAgents)
        {
            result.AppendLine($"### {agent.Name}");
            result.AppendLine($"**Domain:** {agent.Domain}\n");
            result.AppendLine("**Capabilities:**");
            foreach (var capability in agent.Capabilities)
            {
                result.AppendLine($"- {capability}");
            }
            result.AppendLine();
        }

        return result.ToString();
    }

    public List<SubAgentInfo> GetAvailableSubAgents()
    {
        return _subAgents
            .Select(sa => new SubAgentInfo
            {
                Name = sa.Name,
                Domain = sa.Domain,
                Capabilities = sa.Capabilities,
            })
            .ToList();
    }
}
