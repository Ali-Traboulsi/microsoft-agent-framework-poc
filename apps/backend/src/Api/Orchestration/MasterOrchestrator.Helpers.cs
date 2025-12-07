using System.Text;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Middleware;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

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

    /// <summary>
    /// Convert middleware delegation event to orchestrator response
    /// </summary>
    private OrchestratorResponse ConvertDelegationEventToResponse(DelegationEvent delegationEvent)
    {
        return delegationEvent.Type switch
        {
            DelegationEventType.SubAgentDelegationStart => new OrchestratorResponse
            {
                Type = ResponseType.SubAgentDelegation,
                SubAgentName = delegationEvent.SubAgentName,
                Content = $"🔄 Delegating to **{delegationEvent.SubAgentName}**...",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                    ["functionName"] = delegationEvent.FunctionName ?? "",
                },
            },
            DelegationEventType.SubAgentDelegationComplete => new OrchestratorResponse
            {
                Type = ResponseType.SubAgentComplete,
                SubAgentName = delegationEvent.SubAgentName,
                Content = $"✅ **{delegationEvent.SubAgentName}** completed",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                    ["duration"] = (
                        delegationEvent.Timestamp - (delegationEvent.Timestamp)
                    ).TotalMilliseconds,
                },
            },
            DelegationEventType.SubAgentDelegationError => new OrchestratorResponse
            {
                Type = ResponseType.SubAgentComplete,
                SubAgentName = delegationEvent.SubAgentName,
                Content = $"❌ **{delegationEvent.SubAgentName}** error: {delegationEvent.Error}",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                    ["error"] = delegationEvent.Error ?? "",
                },
            },
            DelegationEventType.ToolExecutionStart => new OrchestratorResponse
            {
                Type = ResponseType.ToolExecution,
                ToolName = delegationEvent.ToolName,
                Content = $"🔧 Executing **{delegationEvent.ToolName}**...",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                },
            },
            DelegationEventType.ToolExecutionComplete => new OrchestratorResponse
            {
                Type = ResponseType.ToolExecution,
                ToolName = delegationEvent.ToolName,
                Content = $"✅ Tool **{delegationEvent.ToolName}** completed",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                },
            },
            DelegationEventType.ToolExecutionError => new OrchestratorResponse
            {
                Type = ResponseType.ToolExecution,
                ToolName = delegationEvent.ToolName,
                Content = $"❌ Tool **{delegationEvent.ToolName}** error: {delegationEvent.Error}",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                    ["error"] = delegationEvent.Error ?? "",
                },
            },
            DelegationEventType.WorkflowStepStart => new OrchestratorResponse
            {
                Type = ResponseType.StepStart,
                StepId = delegationEvent.StepId,
                StepName = delegationEvent.StepName,
                StepNameAr = delegationEvent.StepNameAr,
                StepNumber = delegationEvent.StepNumber,
                TotalSteps = delegationEvent.TotalSteps,
                Content = $"⏳ {delegationEvent.StepName}",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                },
            },
            DelegationEventType.WorkflowStepComplete => new OrchestratorResponse
            {
                Type = ResponseType.StepComplete,
                StepId = delegationEvent.StepId,
                StepName = delegationEvent.StepName,
                StepNameAr = delegationEvent.StepNameAr,
                StepNumber = delegationEvent.StepNumber,
                TotalSteps = delegationEvent.TotalSteps,
                StepDurationMs = delegationEvent.StepDurationMs,
                StepDetails = delegationEvent.StepDetails,
                Content = $"✅ {delegationEvent.StepName}",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                    ["durationMs"] = delegationEvent.StepDurationMs ?? 0,
                    ["details"] = delegationEvent.StepDetails ?? "",
                },
            },
            DelegationEventType.WorkflowProgress => new OrchestratorResponse
            {
                Type = ResponseType.Progress,
                StepId = delegationEvent.StepId,
                StepName = delegationEvent.StepName,
                StepNameAr = delegationEvent.StepNameAr,
                StepNumber = delegationEvent.StepNumber,
                TotalSteps = delegationEvent.TotalSteps,
                StepDetails = delegationEvent.StepDetails,
                Content = delegationEvent.StepDetails ?? $"📊 {delegationEvent.StepName}",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                },
            },
            _ => new OrchestratorResponse { Type = ResponseType.Content, Content = "" },
        };
    }

    private AIAgent CreateMasterAgentWithSchemaAndTools(JsonElement schema)
    {
        // Create ChatOptions with structured output
        var chatOptions = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                schema: schema,
                schemaName: "ThinkingModeResponse",
                schemaDescription: "Response with thinking and content"
            ),
        };

        // Build your tools as AIFunctions
        var delegateToSubAgentTool = AIFunctionFactory.Create(DelegateToSubAgent);
        var delegateToMultiTool = AIFunctionFactory.Create(DelegateToMultipleSubAgents);
        var listAgentsTool = AIFunctionFactory.Create(GetAvailableSubAgentsAsString);
        var webSearchTool = AIFunctionFactory.Create(_webSearchTools.SearchWeb);

        // Put your tools into a list of AITool
        var toolList = new List<AITool>
        {
            delegateToSubAgentTool,
            delegateToMultiTool,
            listAgentsTool,
            webSearchTool,
        };

        // Now use the ChatClientAgentOptions constructor that accepts tools
        var agentOptions = new ChatClientAgentOptions(
            instructions: AgentInstructionsLoader.LoadMasterAgentInstructions(_subAgents),
            name: "MasterAgentThinkingWithTools",
            description: null,
            tools: toolList
        );

        // Also set the ChatOptions (structured output) on the agentOptions
        agentOptions.ChatOptions = chatOptions;

        // Finally create the agent with proper logging
        return new ChatClientAgent(
            _chatClient,
            agentOptions,
            loggerFactory: _loggerFactory,
            services: null // IServiceProvider not needed for basic scenarios
        );
    }
}
