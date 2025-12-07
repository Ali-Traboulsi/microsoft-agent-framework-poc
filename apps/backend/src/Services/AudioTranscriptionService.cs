using System.Net.Http.Headers;

namespace AgentFrameworkQuickStart.Services;

/// <summary>
/// Service for transcribing audio files using OpenAI Whisper API
/// </summary>
public class AudioTranscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public AudioTranscriptionService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey =
            configuration["OpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("OpenAI API key not configured");
    }

    /// <summary>
    /// Transcribes audio file to text using OpenAI Whisper
    /// </summary>
    public async Task<string> TranscribeAudioAsync(
        byte[] audioData,
        string fileName,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var content = new MultipartFormDataContent();

            // Add audio file
            var audioContent = new ByteArrayContent(audioData);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue(
                GetAudioMediaType(fileName)
            );
            content.Add(audioContent, "file", fileName);

            // Add model parameter
            content.Add(new StringContent("whisper-1"), "model");

            // Add language parameter (optional - auto-detect if not specified)
            // content.Add(new StringContent("en"), "language");

            // Call OpenAI Whisper API
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/audio/transcriptions"
            );
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            // Parse JSON response
            var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);
            var transcript = jsonDoc.RootElement.GetProperty("text").GetString() ?? "";

            return transcript;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to transcribe audio: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Determines if a file is an audio file based on media type
    /// </summary>
    public static bool IsAudioFile(string mediaType)
    {
        return mediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets audio media type from filename
    /// </summary>
    private static string GetAudioMediaType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".mp3" => "audio/mpeg",
            ".mp4" or ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".webm" => "audio/webm",
            ".ogg" => "audio/ogg",
            ".flac" => "audio/flac",
            _ => "audio/mpeg",
        };
    }
}
