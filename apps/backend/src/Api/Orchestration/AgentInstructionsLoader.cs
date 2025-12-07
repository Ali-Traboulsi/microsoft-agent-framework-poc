using System.Text;
using AgentFrameworkQuickStart.Api.Abstractions;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Helper for loading and formatting agent instructions
/// </summary>
public static class AgentInstructionsLoader
{
    private static readonly string InstructionsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Api",
        "Orchestration",
        "Instructions"
    );

    /// <summary>
    /// Load master agent instructions with sub-agent descriptions
    /// </summary>
    public static string LoadMasterAgentInstructions(IEnumerable<ISubAgent> subAgents)
    {
        var instructionsFile = Path.Combine(InstructionsPath, "MasterAgentInstructions.txt");
        var template = File.ReadAllText(instructionsFile);

        var subAgentDescriptions = string.Join(
            "\n",
            subAgents.Select(sa =>
                $"**{sa.Name}** - {sa.Domain}\n"
                + $"  Capabilities: {string.Join(", ", sa.Capabilities)}"
            )
        );

        return template.Replace("{SUB_AGENT_DESCRIPTIONS}", subAgentDescriptions);
    }

    /// <summary>
    /// Load structured agent instructions with sub-agent descriptions
    /// </summary>
    public static string LoadStructuredAgentInstructions(IEnumerable<ISubAgent> subAgents)
    {
        var instructionsFile = Path.Combine(InstructionsPath, "StructuredAgentInstructions.txt");
        var template = File.ReadAllText(instructionsFile);

        var subAgentDescriptions = string.Join(
            "\n",
            subAgents.Select(sa => $"- **{sa.Name}**: {sa.Domain}")
        );

        return template.Replace("{SUB_AGENT_DESCRIPTIONS}", subAgentDescriptions);
    }

    /// <summary>
    /// Load profit projection sub-agent instructions
    /// </summary>
    public static string LoadProjectionSubAgentInstructions()
    {
        var instructionsFile = Path.Combine(InstructionsPath, "ProjectionSubAgentInstructions.txt");
        return File.ReadAllText(instructionsFile);
    }
}
