using System.Diagnostics;
using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.SubAgents;

/// <summary>
/// Sub-agent specialized in compliance and regulatory oversight
/// </summary>
public class ComplianceOfficerSubAgent : ISubAgent
{
    private readonly AccountTools _accountTools;
    private readonly PortfolioTools _portfolioTools;
    private readonly MutualFundTools _fundTools;
    private readonly SubAgentThreadManager _threadManager;
    private readonly ILogger<ComplianceOfficerSubAgent> _logger;
    private readonly Lazy<AIAgent> _agent;

    public string Name => "ComplianceOfficer";
    public string Domain => "Compliance & Risk Management";
    public string[] Capabilities =>
        new[]
        {
            "Verify transactions comply with investment limits",
            "Check risk appropriateness for client profiles",
            "Ensure proper documentation requirements",
            "Assess portfolio risk levels",
            "Review regulatory compliance",
            "Provide compliance guidance and recommendations",
        };

    public ComplianceOfficerSubAgent(
        IChatClient chatClient,
        AccountTools accountTools,
        PortfolioTools portfolioTools,
        MutualFundTools fundTools,
        SubAgentThreadManager threadManager,
        ILogger<ComplianceOfficerSubAgent> logger
    )
    {
        _accountTools = accountTools;
        _portfolioTools = portfolioTools;
        _fundTools = fundTools;
        _threadManager = threadManager;
        _logger = logger;
        _agent = new Lazy<AIAgent>(() => CreateAgent(chatClient));
    }

    private AIAgent CreateAgent(IChatClient chatClient)
    {
        return chatClient.CreateAIAgent(
            name: Name,
            instructions: $@"You are a Compliance Officer ensuring regulatory requirements and risk management.

**Your Domain:** {Domain}

**Your Capabilities:**
{string.Join("\n", Capabilities.Select(c => $"- {c}"))}

**Important Guidelines:**
1. Focus on compliance, risk assessment, and regulatory matters
2. Be thorough but not obstructive
3. Clearly identify compliance issues or violations
4. Provide specific remediation recommendations
5. Explain regulatory requirements in understandable terms
6. Balance risk management with business objectives
7. For non-compliance matters, indicate clearly

**Response Format:**
- Start with compliance status (✅ Compliant / ⚠️ Review Needed / ❌ Violation)
- Detail findings and rationale
- List any regulatory requirements
- Provide clear recommendations or next steps",
            tools:
            [
                AIFunctionFactory.Create(_accountTools.GetAccountBalance),
                AIFunctionFactory.Create(_portfolioTools.GetPortfolioDetails),
                AIFunctionFactory.Create(_portfolioTools.GetPortfolioAllocation),
                AIFunctionFactory.Create(_fundTools.GetFundDetails),
            ]
        );
    }

    public async Task<SubAgentResponse> HandleRequestAsync(
        string request,
        string conversationId,
        Dictionary<string, object>? context = null
    )
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "{SubAgent} handling request for conversation {ConversationId}: {Request}",
                Name,
                conversationId,
                request
            );

            // Get shared conversation context from previous sub-agent interactions
            var conversationContext = await _threadManager.GetConversationContextAsync(
                conversationId
            );

            // Build the full request with context if available
            var fullRequest = string.IsNullOrEmpty(conversationContext)
                ? request
                : $"{conversationContext}\n\nCurrent request: {request}";

            var thread = _threadManager.GetOrCreateThread(conversationId, Name, _agent.Value);
            var result = await _agent.Value.RunAsync(fullRequest, thread);
            var responseText = result.Messages.LastOrDefault()?.Text ?? "No response generated";

            // Add this interaction to shared conversation memory
            _threadManager.AddMemory(conversationId, Name, request, responseText);

            sw.Stop();

            _logger.LogInformation(
                "{SubAgent} completed in {Duration}ms",
                Name,
                sw.ElapsedMilliseconds
            );

            return new SubAgentResponse
            {
                SubAgentName = Name,
                Success = true,
                Result = responseText,
                ToolsUsed = new List<string>(),
                DurationMs = sw.ElapsedMilliseconds,
                Metadata = context ?? new(),
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            _logger.LogError(
                ex,
                "{SubAgent} error after {Duration}ms: {Error}",
                Name,
                sw.ElapsedMilliseconds,
                ex.Message
            );

            return new SubAgentResponse
            {
                SubAgentName = Name,
                Success = false,
                ErrorMessage = ex.Message,
                DurationMs = sw.ElapsedMilliseconds,
                ToolsUsed = new List<string>(),
                Metadata = context ?? new(),
            };
        }
    }

    public async IAsyncEnumerable<SubAgentStreamChunk> HandleRequestStreamingAsync(
        string request,
        string conversationId,
        Dictionary<string, object>? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "{SubAgent} handling streaming request for conversation {ConversationId}: {Request}",
            Name,
            conversationId,
            request
        );

        // Get shared conversation context from previous sub-agent interactions
        var conversationContext = await _threadManager.GetConversationContextAsync(conversationId);

        // Build the full request with context if available
        var fullRequest = string.IsNullOrEmpty(conversationContext)
            ? request
            : $"{conversationContext}\n\nCurrent request: {request}";

        var thread = _threadManager.GetOrCreateThread(conversationId, Name, _agent.Value);
        var responseBuilder = new System.Text.StringBuilder();

        await foreach (var chunk in _agent.Value.RunStreamingAsync(fullRequest, thread))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (chunk.Text != null)
            {
                responseBuilder.Append(chunk.Text);
                yield return new SubAgentStreamChunk { Text = chunk.Text, IsComplete = false };
            }
        }

        // Add this interaction to shared conversation memory
        _threadManager.AddMemory(conversationId, Name, request, responseBuilder.ToString());

        _logger.LogInformation("{SubAgent} streaming completed", Name);

        yield return new SubAgentStreamChunk { IsComplete = true };
    }
}
