using System.Diagnostics;
using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.SubAgents;

/// <summary>
/// Sub-agent specialized in profit projection calculations
/// احتساب الأرباح التقديرية
/// </summary>
public class ProfitProjectionSubAgent : ISubAgent
{
    private readonly IChatClient _chatClient;
    private readonly ProjectionTools _projectionTools;
    private readonly ILogger<ProfitProjectionSubAgent> _logger;
    private readonly Lazy<AIAgent> _agent;

    public string Name => "ProfitProjection";
    public string Domain => "Investment Profit Projections";
    public string[] Capabilities =>
        new[]
        {
            "Calculate expected profit projections for investments",
            "Provide conservative, expected, and optimistic return scenarios",
            "Analyze fund performance and recommend suitable allocations",
            "Compare lump sum vs. monthly SIP investment strategies",
            "Calculate personalized projections for existing customers",
            "Support Shariah-compliant investment projections",
            "Provide quick estimates for investment returns",
        };

    public ProfitProjectionSubAgent(
        IChatClient chatClient,
        ProjectionTools projectionTools,
        ILogger<ProfitProjectionSubAgent> logger
    )
    {
        _chatClient = chatClient;
        _projectionTools = projectionTools;
        _logger = logger;
        _agent = new Lazy<AIAgent>(CreateAgent);
    }

    private AIAgent CreateAgent()
    {
        return _chatClient.CreateAIAgent(
            name: Name,
            instructions: $@"You are a specialized Profit Projection Expert for investment banking.

**Your Domain:** {Domain}

**Your Capabilities:**
{string.Join("\n", Capabilities.Select(c => $"- {c}"))}

**Important Guidelines:**

1. **Gather Required Information**
   When a user wants a profit projection, you need these details:
   - **Investment Amount**: How much they want to invest (required)
   - **Time Horizon**: How long they plan to invest (in months, required)
   - **Risk Profile**: Conservative, Moderate, or Aggressive (required)
   - **Currency**: SAR, USD, EUR (default: SAR)
   - **Shariah Compliance**: Whether they want only Shariah-compliant funds
   - **Customer ID**: If they're an existing customer (optional, for personalized projections)

2. **Ask Questions When Needed**
   If the user doesn't provide all required information, ask them:
   - ""How much would you like to invest?""
   - ""For how long do you plan to invest (in months or years)?""
   - ""What's your risk tolerance - Conservative, Moderate, or Aggressive?""
   - ""Do you prefer Shariah-compliant funds only?""

3. **Use Your Tools**
   Once you have the required information:
   - Use `CalculateProfitProjection` for anonymous/new customer projections
   - Use `CalculatePersonalizedProjection` when you have a Customer ID (CIF)
   - Use `CompareInvestmentStrategies` when they want to compare lump sum vs SIP
   - Use `GetQuickEstimate` for simple/fast estimates

4. **Present Results Clearly**
   When presenting projection results:
   - Start with a summary of key findings
   - Show all three scenarios (Conservative, Expected, Optimistic)
   - Explain the recommended fund allocations
   - Highlight important risks and considerations
   - Provide clear next steps or call-to-action

5. **Handle Edge Cases**
   - If investment amount is too low, suggest minimum thresholds
   - If time horizon is very short (<6 months), explain limitations
   - If they want Shariah-compliant and none are found, explain alternatives

**Response Format:**
- Use clear headings and bullet points
- Include both English and Arabic terms where appropriate
- Show monetary values with proper formatting
- Present percentages and returns clearly
- Always end with actionable recommendations

**Example Interaction:**
User: ""I want to invest 100,000 SAR for 2 years""
You: First confirm risk profile, then calculate projection and present results.

User: ""How much can I earn from a 50,000 investment?""
You: Ask about time horizon and risk tolerance, then provide projection.",
            tools:
            [
                AIFunctionFactory.Create(_projectionTools.CalculateProfitProjection),
                AIFunctionFactory.Create(_projectionTools.CalculatePersonalizedProjection),
                AIFunctionFactory.Create(_projectionTools.CompareInvestmentStrategies),
                AIFunctionFactory.Create(_projectionTools.GetQuickEstimate),
            ]
        );
    }

    public async Task<SubAgentResponse> HandleRequestAsync(
        string request,
        Dictionary<string, object>? context = null
    )
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("{SubAgent} handling request: {Request}", Name, request);

            var result = await _agent.Value.RunAsync(request);
            var responseText = result.Messages.LastOrDefault()?.Text ?? "No response generated";

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
                ToolsUsed = new List<string>(), // Tools tracked by framework
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
        Dictionary<string, object>? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("{SubAgent} handling streaming request: {Request}", Name, request);

        await foreach (var chunk in _agent.Value.RunStreamingAsync(request))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (chunk.Text != null)
            {
                yield return new SubAgentStreamChunk { Text = chunk.Text, IsComplete = false };
            }
        }

        yield return new SubAgentStreamChunk { IsComplete = true };
    }
}
