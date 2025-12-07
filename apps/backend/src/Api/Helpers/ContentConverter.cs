using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Services;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Helpers;

/// <summary>
/// Helper class to convert ContentInput DTOs to AIContent types
/// </summary>
public static class ContentConverter
{
    /// <summary>
    /// Converts a list of ContentInput DTOs to AIContent list
    /// </summary>
    public static List<AIContent> ConvertToAIContents(
        List<ContentInput> inputs,
        string? textMessage = null
    )
    {
        var contents = new List<AIContent>();

        // Add text message if provided
        if (!string.IsNullOrEmpty(textMessage))
        {
            contents.Add(new TextContent(textMessage));
        }

        // Convert each content input
        foreach (var input in inputs)
        {
            var content = ConvertToAIContent(input);
            if (content != null)
            {
                contents.Add(content);
            }
        }

        return contents;
    }

    /// <summary>
    /// Converts a single ContentInput DTO to AIContent
    /// </summary>
    public static AIContent? ConvertToAIContent(ContentInput input)
    {
        return input.Type.ToLowerInvariant() switch
        {
            "text" when !string.IsNullOrEmpty(input.Text) => new TextContent(input.Text),

            "image" when !string.IsNullOrEmpty(input.Data) => CreateDataContent(
                input.Data,
                input.MediaType ?? "image/*"
            ),

            "audio" when !string.IsNullOrEmpty(input.Data) => CreateDataContent(
                input.Data,
                input.MediaType ?? "audio/*"
            ),

            "uri" when !string.IsNullOrEmpty(input.Uri) => new UriContent(
                new Uri(input.Uri),
                input.MediaType ?? "application/octet-stream"
            ),

            "file" when !string.IsNullOrEmpty(input.Data) => CreateDataContent(
                input.Data,
                input.MediaType ?? "application/octet-stream"
            ),

            _ => null,
        };
    }

    /// <summary>
    /// Creates DataContent from base64 string or data URI
    /// </summary>
    private static DataContent CreateDataContent(string data, string mediaType)
    {
        // Check if data is already a data URI
        if (data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return new DataContent(data, mediaType);
        }

        // Otherwise, assume it's base64 and create data URI
        var dataUri = $"data:{mediaType};base64,{data}";
        return new DataContent(dataUri, mediaType);
    }

    /// <summary>
    /// Validates content input
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateContentInput(ContentInput input)
    {
        if (string.IsNullOrEmpty(input.Type))
        {
            return (false, "Content type is required");
        }

        return input.Type.ToLowerInvariant() switch
        {
            "text" when string.IsNullOrEmpty(input.Text) => (
                false,
                "Text content requires 'Text' property"
            ),

            "image" when string.IsNullOrEmpty(input.Data) && string.IsNullOrEmpty(input.Uri) => (
                false,
                "Image content requires 'Data' or 'Uri' property"
            ),

            "audio" when string.IsNullOrEmpty(input.Data) && string.IsNullOrEmpty(input.Uri) => (
                false,
                "Audio content requires 'Data' or 'Uri' property"
            ),

            "uri" when string.IsNullOrEmpty(input.Uri) => (
                false,
                "URI content requires 'Uri' property"
            ),

            "file" when string.IsNullOrEmpty(input.Data) && string.IsNullOrEmpty(input.Uri) => (
                false,
                "File content requires 'Data' or 'Uri' property"
            ),

            "text" or "image" or "audio" or "uri" or "file" => (true, null),

            _ => (false, $"Unknown content type: {input.Type}"),
        };
    }

    /// <summary>
    /// Detects media type from file extension
    /// </summary>
    public static string DetectMediaType(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return "application/octet-stream";
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",

            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".ogg" => "audio/ogg",
            ".flac" => "audio/flac",

            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".csv" => "text/csv",

            _ => "application/octet-stream",
        };
    }

    /// <summary>
    /// Converts uploaded form files to AIContent list
    /// </summary>
    public static async Task<List<AIContent>> ConvertFormFilesToAIContents(
        List<IFormFile>? files,
        string? textMessage = null,
        string? uris = null,
        AudioTranscriptionService? audioService = null
    )
    {
        var contents = new List<AIContent>();

        // Add text message if provided
        if (!string.IsNullOrEmpty(textMessage))
        {
            contents.Add(new TextContent(textMessage));
        }

        // Process uploaded files
        if (files != null && files.Count > 0)
        {
            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    // Read file into memory
                    using var memoryStream = new MemoryStream();
                    await file.CopyToAsync(memoryStream);
                    var fileBytes = memoryStream.ToArray();

                    // Detect media type from filename
                    var mediaType = DetectMediaType(file.FileName);

                    // Handle audio files - transcribe to text using Whisper
                    if (AudioTranscriptionService.IsAudioFile(mediaType))
                    {
                        if (audioService == null)
                        {
                            throw new InvalidOperationException(
                                "Audio transcription service is required for audio files"
                            );
                        }

                        var transcript = await audioService.TranscribeAudioAsync(
                            fileBytes,
                            file.FileName
                        );

                        // Add transcript as text content with context
                        contents.Add(
                            new TextContent(
                                $"[Audio transcription from {file.FileName}]: {transcript}"
                            )
                        );
                    }
                    // Handle images and other files - convert to base64 data URI
                    else
                    {
                        // Convert to base64
                        var base64Data = Convert.ToBase64String(fileBytes);

                        // Create data URI
                        var dataUri = $"data:{mediaType};base64,{base64Data}";

                        // Add as DataContent
                        contents.Add(new DataContent(dataUri, mediaType));
                    }
                }
            }
        }

        // Process URIs if provided
        if (!string.IsNullOrEmpty(uris))
        {
            var uriList = uris.Split(
                    new[] { ',', ';', '\n' },
                    StringSplitOptions.RemoveEmptyEntries
                )
                .Select(u => u.Trim())
                .Where(u => Uri.IsWellFormedUriString(u, UriKind.Absolute));

            foreach (var uriString in uriList)
            {
                var uri = new Uri(uriString);
                var mediaType = DetectMediaType(Path.GetFileName(uri.LocalPath));
                contents.Add(new UriContent(uri, mediaType));
            }
        }

        return contents;
    }
}
