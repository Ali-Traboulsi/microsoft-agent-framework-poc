using System.Text;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace AgentFrameworkQuickStart.Examples;

/// <summary>
/// Example demonstrating multi-modal input with the Master Agent
/// Uses the unified streaming API for all operations
/// </summary>
public static class MultiModalExample
{
    /// <summary>
    /// Example: Analyze a simple red square image (streaming)
    /// </summary>
    public static async Task RunImageAnalysisExample(IServiceProvider services)
    {
        Console.WriteLine("\n=== Multi-Modal Example: Image Analysis ===\n");

        var orchestrator = services.GetRequiredService<IMasterOrchestrator>();

        // Create a simple 1x1 red pixel PNG image (base64 encoded)
        var redPixelBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==";

        var contentInputs = new List<ContentInput>
        {
            new()
            {
                Type = "image",
                Data = $"data:image/png;base64,{redPixelBase64}",
                MediaType = "image/png",
            },
        };

        var aiContents = Api.Helpers.ContentConverter.ConvertToAIContents(
            contentInputs,
            "What color is this image? Describe what you see."
        );

        var request = new UnifiedChatRequest
        {
            ConversationId = Guid.NewGuid().ToString(),
            Message = "What color is this image? Describe what you see.",
            Contents = aiContents,
        };

        try
        {
            Console.WriteLine(
                $"📤 Sending multi-modal request with {aiContents.Count} content items"
            );
            Console.WriteLine($"   Message: {request.Message}\n");

            Console.WriteLine("🔄 Streaming response:");
            var responseBuilder = new StringBuilder();
            UnifiedChatResponse? finalResult = null;

            await foreach (var chunk in orchestrator.ProcessAsync(request))
            {
                switch (chunk.Type)
                {
                    case StreamingChunkType.Content:
                        Console.Write(chunk.Content);
                        responseBuilder.Append(chunk.Content);
                        break;

                    case StreamingChunkType.Complete:
                        finalResult = chunk.FinalResult;
                        break;
                }
            }

            Console.WriteLine($"\n\n✅ Response received in {finalResult?.TotalDurationMs}ms\n");

            if (finalResult?.SubAgentsUsed.Count > 0)
            {
                Console.WriteLine(
                    $"📋 Sub-agents used: {string.Join(", ", finalResult.SubAgentsUsed)}"
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"   {ex.GetType().Name}");
        }
    }

    /// <summary>
    /// Example: Streaming multi-modal response with thinking enabled
    /// </summary>
    public static async Task RunStreamingImageAnalysisExample(IServiceProvider services)
    {
        Console.WriteLine("\n=== Multi-Modal Streaming Example ===\n");

        var orchestrator = services.GetRequiredService<IMasterOrchestrator>();

        // Blue square image (1x1 pixel)
        var bluePixelBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPj/HwADBwIAMCbHYQAAAABJRU5ErkJggg==";

        var contentInputs = new List<ContentInput>
        {
            new()
            {
                Type = "image",
                Data = $"data:image/png;base64,{bluePixelBase64}",
                MediaType = "image/png",
            },
        };

        var aiContents = Api.Helpers.ContentConverter.ConvertToAIContents(
            contentInputs,
            "Describe the color and any patterns in this image."
        );

        var request = new UnifiedChatRequest
        {
            ConversationId = Guid.NewGuid().ToString(),
            Message = "Describe the color and any patterns in this image.",
            Contents = aiContents,
            EnableThinking = true,
        };

        try
        {
            Console.WriteLine($"📤 Starting streaming multi-modal request");
            Console.WriteLine($"   Message: {request.Message}\n");

            Console.WriteLine("🔄 Streaming response:");

            await foreach (var chunk in orchestrator.ProcessAsync(request))
            {
                switch (chunk.Type)
                {
                    case StreamingChunkType.Thinking:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write(chunk.Content);
                        Console.ResetColor();
                        break;

                    case StreamingChunkType.Content:
                        Console.Write(chunk.Content);
                        break;

                    case StreamingChunkType.ToolExecution:
                        Console.WriteLine($"\n🔧 [Tool: {chunk.ToolName}]");
                        break;

                    case StreamingChunkType.Complete:
                        Console.WriteLine(
                            $"\n\n✅ Streaming complete ({chunk.FinalResult?.TotalDurationMs}ms)"
                        );
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Example: Mixed content (text + image)
    /// </summary>
    public static async Task RunMixedContentExample(IServiceProvider services)
    {
        Console.WriteLine("\n=== Multi-Modal Example: Mixed Content ===\n");

        var orchestrator = services.GetRequiredService<IMasterOrchestrator>();

        // Green square (1x1 pixel)
        var greenPixelBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAEBgIApD5fRAAAAABJRU5ErkJggg==";

        var contentInputs = new List<ContentInput>
        {
            new()
            {
                Type = "text",
                Text =
                    "I'm analyzing a portfolio performance indicator. Please help me interpret it.",
            },
            new()
            {
                Type = "image",
                Data = $"data:image/png;base64,{greenPixelBase64}",
                MediaType = "image/png",
                FileName = "indicator.png",
            },
            new()
            {
                Type = "text",
                Text = "What does the color typically signify in financial dashboards?",
            },
        };

        var aiContents = Api.Helpers.ContentConverter.ConvertToAIContents(
            contentInputs,
            string.Empty
        );

        var request = new UnifiedChatRequest
        {
            ConversationId = Guid.NewGuid().ToString(),
            Contents = aiContents,
        };

        try
        {
            Console.WriteLine("📤 Sending mixed content request:");
            foreach (var content in contentInputs)
            {
                if (content.Type == "text")
                {
                    Console.WriteLine($"   - Text: {content.Text}");
                }
                else
                {
                    Console.WriteLine($"   - {content.Type}: {content.FileName ?? "untitled"}");
                }
            }

            Console.WriteLine("\n🔄 Streaming response:");
            UnifiedChatResponse? finalResult = null;

            await foreach (var chunk in orchestrator.ProcessAsync(request))
            {
                if (chunk.Type == StreamingChunkType.Content)
                {
                    Console.Write(chunk.Content);
                }
                else if (chunk.Type == StreamingChunkType.Complete)
                {
                    finalResult = chunk.FinalResult;
                }
            }

            Console.WriteLine($"\n\n⏱️  Processing time: {finalResult?.TotalDurationMs}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Example: URI-based content
    /// </summary>
    public static async Task RunUriContentExample(IServiceProvider services)
    {
        Console.WriteLine("\n=== Multi-Modal Example: URI Content ===\n");

        var orchestrator = services.GetRequiredService<IMasterOrchestrator>();

        var contentInputs = new List<ContentInput>
        {
            new() { Type = "uri", Uri = "https://example.com/portfolio-chart.png" },
        };

        var aiContents = Api.Helpers.ContentConverter.ConvertToAIContents(
            contentInputs,
            "Based on the image at this URL, what investment insights can you provide?"
        );

        var request = new UnifiedChatRequest
        {
            ConversationId = Guid.NewGuid().ToString(),
            Message = "Based on the image at this URL, what investment insights can you provide?",
            Contents = aiContents,
        };

        try
        {
            Console.WriteLine("📤 Sending URI-based request");
            Console.WriteLine($"   URI: {contentInputs[0].Uri}\n");

            Console.WriteLine("🔄 Streaming response:");

            await foreach (var chunk in orchestrator.ProcessAsync(request))
            {
                if (chunk.Type == StreamingChunkType.Content)
                {
                    Console.Write(chunk.Content);
                }
                else if (chunk.Type == StreamingChunkType.Complete)
                {
                    Console.WriteLine($"\n\n✅ Complete ({chunk.FinalResult?.TotalDurationMs}ms)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine("   Note: URI content requires network access and valid URL");
        }
    }

    /// <summary>
    /// Run all multi-modal examples
    /// </summary>
    public static async Task RunAllExamples(IServiceProvider services)
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       Multi-Modal Input Examples - Master Agent           ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

        await RunImageAnalysisExample(services);
        await Task.Delay(1000); // Brief pause between examples

        await RunStreamingImageAnalysisExample(services);
        await Task.Delay(1000);

        await RunMixedContentExample(services);
        await Task.Delay(1000);

        await RunUriContentExample(services);

        Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║            All Multi-Modal Examples Completed             ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");
    }
}
