using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Helpers;
using AgentFrameworkQuickStart.Services;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Hubs.Handlers;

/// <summary>
/// Handles multi-modal chat streaming with support for images, audio, and documents
/// </summary>
public class MultiModalChatHandler(
    IMasterOrchestrator orchestrator,
    AudioTranscriptionService audioService
) : IMultiModalChatHandler
{
    public async IAsyncEnumerable<MasterStreamingResponse> StreamAsync(
        MultiModalChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // Validate request
        var validationResult = ValidateRequest(request);
        if (validationResult != null)
        {
            yield return validationResult;
            yield break;
        }

        // Process contents and build AI content list
        var aiContents = new List<AIContent>();

        if (!string.IsNullOrEmpty(request.Message))
            aiContents.Add(new TextContent(request.Message));

        await foreach (var result in ProcessContentsAsync(request.Contents!, cancellationToken))
        {
            if (result.Response != null)
                yield return result.Response;

            if (result.Content != null)
                aiContents.Add(result.Content);
        }

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();

        var unifiedRequest = new UnifiedChatRequest
        {
            ConversationId = conversationId,
            Message = request.Message,
            Contents = aiContents,
            EnableThinking = request.EnableThinking,
            CancellationToken = cancellationToken,
        };

        await foreach (var chunk in orchestrator.ProcessAsync(unifiedRequest, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            yield return ChatStreamHandler.MapChunkToStreamingResponse(chunk);
        }
    }

    private static MasterStreamingResponse? ValidateRequest(MultiModalChatRequest request)
    {
        if (request.Contents == null || request.Contents.Count == 0)
        {
            return new MasterStreamingResponse
            {
                Type = ResponseType.Error.ToString(),
                Content = "Request must include at least one content item",
                IsComplete = true,
            };
        }

        foreach (var content in request.Contents)
        {
            var (isValid, errorMessage) = ContentConverter.ValidateContentInput(content);
            if (!isValid)
            {
                return new MasterStreamingResponse
                {
                    Type = ResponseType.Error.ToString(),
                    Content = errorMessage,
                    IsComplete = true,
                };
            }
        }

        return null;
    }

    private async IAsyncEnumerable<ContentProcessResult> ProcessContentsAsync(
        List<ContentInput> contents,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        foreach (var content in contents)
        {
            if (IsAudioContent(content))
            {
                var result = await ProcessAudioContentAsync(content);
                yield return result;
            }
            else
            {
                var aiContent = ContentConverter.ConvertToAIContent(content);
                if (aiContent != null)
                    yield return new ContentProcessResult(aiContent);
            }
        }
    }

    private static bool IsAudioContent(ContentInput content) =>
        content.Type?.ToLowerInvariant() == "audio"
        && !string.IsNullOrEmpty(content.Data)
        && !string.IsNullOrEmpty(content.MediaType)
        && AudioTranscriptionService.IsAudioFile(content.MediaType);

    private async Task<ContentProcessResult> ProcessAudioContentAsync(ContentInput content)
    {
        var base64Data = ExtractBase64Data(content.Data!);
        var audioBytes = Convert.FromBase64String(base64Data);
        var fileName = content.FileName ?? "audio.mp3";

        var transcript = await audioService.TranscribeAudioAsync(audioBytes, fileName);

        var transcriptionResponse = new MasterStreamingResponse
        {
            Type = ResponseType.Transcription.ToString(),
            Content = transcript,
            Metadata = new Dictionary<string, object>
            {
                ["FileName"] = fileName,
                ["FileSize"] = audioBytes.Length,
                ["MediaType"] = content.MediaType ?? "audio/unknown",
            },
            IsComplete = false,
        };

        var textContent = new TextContent($"[Audio file '{fileName}' transcription]: {transcript}");

        return new ContentProcessResult(textContent, transcriptionResponse);
    }

    private static string ExtractBase64Data(string data)
    {
        if (!data.StartsWith("data:"))
            return data;

        var commaIndex = data.IndexOf(',');
        return commaIndex >= 0 ? data[(commaIndex + 1)..] : data;
    }

    private record ContentProcessResult(
        AIContent? Content,
        MasterStreamingResponse? Response = null
    );
}
