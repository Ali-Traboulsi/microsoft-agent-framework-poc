using System.Text.Json;
using AgentFrameworkQuickStart.Api.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace AgentFrameworkQuickStart.Examples;

/// <summary>
/// Example demonstrating multi-modal input with the Master Agent
/// </summary>
public static class MultiModalExample
{
    /// <summary>
    /// Example: Analyze a simple red square image
    /// </summary>
    public static async Task RunImageAnalysisExample(IServiceProvider services)
    {
        Console.WriteLine("\n=== Multi-Modal Example: Image Analysis ===\n");

        var orchestrator = services.GetRequiredService<Api.Abstractions.IMasterOrchestrator>();

        // Create a simple 1x1 red pixel PNG image (base64 encoded)
        // This is a minimal valid PNG for testing
        var redPixelBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==";

        var request = new MultiModalChatRequest
        {
            Message = "What color is this image? Describe what you see.",
            Contents = new List<ContentInput>
            {
                new ContentInput
                {
                    Type = "image",
                    Data = $"data:image/png;base64,{redPixelBase64}",
                    MediaType = "image/png",
                },
            },
            ConversationId = Guid.NewGuid().ToString(),
        };

        try
        {
            // Convert to AIContent
            var aiContents = Api.Helpers.ContentConverter.ConvertToAIContents(
                request.Contents,
                request.Message ?? string.Empty
            );

            Console.WriteLine(
                $"📤 Sending multi-modal request with {aiContents.Count} content items"
            );
            Console.WriteLine($"   Message: {request.Message}");
            Console.WriteLine(
                $"   Content types: {string.Join(", ", request.Contents.Select(c => c.Type))}\n"
            );

            // Process through orchestrator
            var result = await orchestrator.ProcessMultiModalRequestAsync(
                aiContents,
                request.ConversationId!
            );

            Console.WriteLine($"✅ Response received in {result.TotalDurationMs}ms\n");
            Console.WriteLine($"🤖 Agent response:\n{result.Response}\n");

            if (result.SubAgentsUsed.Any())
            {
                Console.WriteLine($"📋 Sub-agents used: {string.Join(", ", result.SubAgentsUsed)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"   {ex.GetType().Name}");
        }
    }

    /// <summary>
    /// Example: Streaming multi-modal response
    /// </summary>
    public static async Task RunStreamingImageAnalysisExample(IServiceProvider services)
    {
        Console.WriteLine("\n=== Multi-Modal Streaming Example ===\n");

        var orchestrator = services.GetRequiredService<Api.Abstractions.IMasterOrchestrator>();

        // Blue square image (1x1 pixel)
        var bluePixelBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPj/HwADBwIAMCbHYQAAAABJRU5ErkJggg==";

        var request = new MultiModalChatRequest
        {
            Message = "Describe the color and any patterns in this image.",
            Contents = new List<ContentInput>
            {
                new ContentInput
                {
                    Type = "image",
                    Data = $"data:image/png;base64,{bluePixelBase64}",
                    MediaType = "image/png",
                },
            },
            ConversationId = Guid.NewGuid().ToString(),
        };

        try
        {
            var aiContents = Api.Helpers.ContentConverter.ConvertToAIContents(
                request.Contents,
                request.Message ?? string.Empty
            );

            Console.WriteLine($"📤 Starting streaming multi-modal request");
            Console.WriteLine($"   Message: {request.Message}\n");

            Console.WriteLine("🔄 Streaming response:");

            await foreach (
                var chunk in orchestrator.ProcessMultiModalRequestStreamingAsync(
                    aiContents,
                    request.ConversationId!
                )
            )
            {
                switch (chunk.Type)
                {
                    case Api.Abstractions.ResponseType.Content:
                        Console.Write(chunk.Content);
                        break;

                    case Api.Abstractions.ResponseType.ToolExecution:
                        Console.WriteLine($"\n🔧 [Tool: {chunk.ToolName}]");
                        break;

                    case Api.Abstractions.ResponseType.Complete:
                        Console.WriteLine(
                            $"\n\n✅ Streaming complete ({chunk.Metadata?["totalDurationMs"]}ms)"
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

        var orchestrator = services.GetRequiredService<Api.Abstractions.IMasterOrchestrator>();

        // Green square (1x1 pixel)
        var greenPixelBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAEBgIApD5fRAAAAABJRU5ErkJggg==";

        var request = new MultiModalChatRequest
        {
            Contents = new List<ContentInput>
            {
                new ContentInput
                {
                    Type = "text",
                    Text =
                        "I'm analyzing a portfolio performance indicator. Please help me interpret it.",
                },
                new ContentInput
                {
                    Type = "image",
                    Data = $"data:image/png;base64,{greenPixelBase64}",
                    MediaType = "image/png",
                    FileName = "indicator.png",
                },
                new ContentInput
                {
                    Type = "text",
                    Text = "What does the color typically signify in financial dashboards?",
                },
            },
            ConversationId = Guid.NewGuid().ToString(),
        };

        try
        {
            var aiContents = Api.Helpers.ContentConverter.ConvertToAIContents(
                request.Contents,
                string.Empty
            );

            Console.WriteLine($"📤 Sending mixed content request:");
            foreach (var content in request.Contents)
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

            Console.WriteLine();

            var result = await orchestrator.ProcessMultiModalRequestAsync(
                aiContents,
                request.ConversationId!
            );

            Console.WriteLine($"🤖 Agent response:\n{result.Response}\n");
            Console.WriteLine($"⏱️  Processing time: {result.TotalDurationMs}ms");
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

        var orchestrator = services.GetRequiredService<Api.Abstractions.IMasterOrchestrator>();

        var request = new MultiModalChatRequest
        {
            Message = "Based on the image at this URL, what investment insights can you provide?",
            Contents = new List<ContentInput>
            {
                new ContentInput { Type = "uri", Uri = "https://example.com/portfolio-chart.png" },
            },
            ConversationId = Guid.NewGuid().ToString(),
        };

        try
        {
            var aiContents = Api.Helpers.ContentConverter.ConvertToAIContents(
                request.Contents,
                request.Message ?? string.Empty
            );

            Console.WriteLine($"📤 Sending URI-based request");
            Console.WriteLine($"   URI: {request.Contents[0].Uri}\n");

            var result = await orchestrator.ProcessMultiModalRequestAsync(
                aiContents,
                request.ConversationId!
            );

            Console.WriteLine($"🤖 Response:\n{result.Response}\n");
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
