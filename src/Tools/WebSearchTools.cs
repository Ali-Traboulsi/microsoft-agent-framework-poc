using System.ComponentModel;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentFrameworkQuickStart.Tools;

/// <summary>
/// Tools for performing web searches using Serper.dev API
/// </summary>
public class WebSearchTools
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebSearchTools> _logger;

    public WebSearchTools(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<WebSearchTools> logger
    )
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [Description(
        "Search the web for current information, news, or answers to questions that require up-to-date data"
    )]
    public async Task<string> SearchWeb(
        [Description("The search query to find information on the web")] string query,
        [Description("Number of results to return (default: 5, max: 10)")] int count = 5
    )
    {
        try
        {
            _logger.LogInformation(
                "Performing web search via Serper.dev for query: {Query}",
                query
            );

            var apiKey =
                _configuration["SerperApi:ApiKey"]
                ?? Environment.GetEnvironmentVariable("SERPER_API_KEY");

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Serper API key not configured");
                return "Error: Web search is not configured. Please set SERPER_API_KEY environment variable or configure SerperApi:ApiKey in appsettings.json";
            }

            // Validate count
            count = Math.Max(1, Math.Min(count, 10));

            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://google.serper.dev/search"
            );
            request.Headers.Add("X-API-KEY", apiKey);

            // Create request body
            var requestBody = new { q = query, num = count };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Serper API request failed with status: {StatusCode}, Error: {Error}",
                    response.StatusCode,
                    errorContent
                );
                return $"Error: Web search failed with status {response.StatusCode}. Please check your API key and subscription.";
            }

            var content = await response.Content.ReadAsStringAsync();
            var searchResponse = JsonSerializer.Deserialize<SerperSearchResponse>(content);

            if (searchResponse?.Organic == null || !searchResponse.Organic.Any())
            {
                return $"No web results found for query: {query}";
            }

            // Format results for the agent
            var results = new StringBuilder();
            results.AppendLine($"Web search results for '{query}':\n");

            var resultNum = 1;
            foreach (var result in searchResponse.Organic.Take(count))
            {
                results.AppendLine($"{resultNum}. {result.Title}");
                results.AppendLine($"   URL: {result.Link}");
                if (!string.IsNullOrEmpty(result.Snippet))
                {
                    results.AppendLine($"   {result.Snippet}");
                }
                results.AppendLine();
                resultNum++;
            }

            _logger.LogInformation(
                "Web search completed successfully with {Count} results",
                resultNum - 1
            );
            return results.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing web search");
            return $"Error: Web search failed - {ex.Message}";
        }
    }
}

/// <summary>
/// Response model for Serper.dev search results
/// </summary>
public class SerperSearchResponse
{
    [JsonPropertyName("organic")]
    public List<SerperSearchResult>? Organic { get; set; }

    [JsonPropertyName("knowledgeGraph")]
    public SerperKnowledgeGraph? KnowledgeGraph { get; set; }
}

/// <summary>
/// Individual search result from Serper.dev
/// </summary>
public class SerperSearchResult
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;

    [JsonPropertyName("snippet")]
    public string? Snippet { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }
}

/// <summary>
/// Knowledge graph information from Serper.dev (optional)
/// </summary>
public class SerperKnowledgeGraph
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
