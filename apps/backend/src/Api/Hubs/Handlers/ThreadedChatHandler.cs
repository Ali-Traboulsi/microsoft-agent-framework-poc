using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Models;

namespace AgentFrameworkQuickStart.Api.Hubs.Handlers;

/// <summary>
/// Handles chat streaming with thread persistence
/// </summary>
public class ThreadedChatHandler(
    IMasterOrchestrator orchestrator,
    IServiceScopeFactory scopeFactory
) : IThreadedChatHandler
{
    public async IAsyncEnumerable<MasterStreamingResponse> StreamAsync(
        string message,
        string? threadIdStr,
        string? conversationId,
        bool enableThinking,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var threadContext = await InitializeThreadAsync(message, threadIdStr, cancellationToken);

        // Send thread ID to client
        yield return new MasterStreamingResponse
        {
            Type = "ThreadCreated",
            Content = threadContext.ThreadId.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["threadId"] = threadContext.ThreadId.ToString(),
            },
            IsComplete = false,
        };

        var actualConversationId = conversationId ?? threadContext.ThreadId.ToString();
        var responseCollector = new ResponseCollector();
        var messageSaved = false;

        var responseStream = GetResponseStream(
            message,
            actualConversationId,
            threadContext.PriorMessages,
            enableThinking
        );

        try
        {
            await foreach (var response in responseStream.WithCancellation(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                responseCollector.Collect(response);

                yield return ChatStreamHandler.MapToStreamingResponse(response);

                if (response.Type == ResponseType.Complete)
                {
                    await SaveAssistantMessageAsync(
                        threadContext.ThreadId,
                        responseCollector,
                        cancellationToken
                    );
                    messageSaved = true;
                }
            }
        }
        finally
        {
            // Ensure assistant message is saved even if stream was interrupted
            // Only save if we have content and haven't already saved
            if (!messageSaved && responseCollector.HasContent)
            {
                using var scope = scopeFactory.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<
                    ILogger<ThreadedChatHandler>
                >();
                logger.LogWarning(
                    "Stream ended without Complete response, saving partial message to thread {ThreadId}",
                    threadContext.ThreadId
                );

                await SaveAssistantMessageAsync(
                    threadContext.ThreadId,
                    responseCollector,
                    CancellationToken.None // Use None since original may be cancelled
                );
            }
        }
    }

    private async Task<ThreadContext> InitializeThreadAsync(
        string message,
        string? threadIdStr,
        CancellationToken cancellationToken
    )
    {
        Guid? parsedThreadId = null;
        if (!string.IsNullOrEmpty(threadIdStr) && Guid.TryParse(threadIdStr, out var parsed))
            parsedThreadId = parsed;

        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Guid actualThreadId;
        var priorMessages = new List<ConversationMessage>();

        if (parsedThreadId.HasValue && parsedThreadId.Value != Guid.Empty)
        {
            actualThreadId = parsedThreadId.Value;
            priorMessages = await LoadPriorMessagesAsync(
                unitOfWork,
                actualThreadId,
                cancellationToken
            );
        }
        else
        {
            var thread = await unitOfWork.Threads.CreateAsync(
                new ChatThread { Title = GenerateThreadTitle(message) },
                cancellationToken
            );
            actualThreadId = thread.Id;
        }

        await unitOfWork.Messages.AddAsync(
            new ChatMessage
            {
                ThreadId = actualThreadId,
                Role = MessageRole.User,
                Content = message,
            },
            cancellationToken
        );

        return new ThreadContext(actualThreadId, priorMessages);
    }

    private static async Task<List<ConversationMessage>> LoadPriorMessagesAsync(
        IUnitOfWork unitOfWork,
        Guid threadId,
        CancellationToken cancellationToken
    )
    {
        var existingThread = await unitOfWork.Threads.GetByIdWithMessagesAsync(
            threadId,
            cancellationToken
        );

        if (existingThread?.Messages == null || existingThread.Messages.Count == 0)
            return [];

        return existingThread
            .Messages.OrderBy(m => m.SequenceNumber)
            .Select(m => new ConversationMessage
            {
                Role = m.Role.ToString().ToLower(),
                Content = m.Content,
                SubAgentName = m.SubAgentName,
            })
            .ToList();
    }

    private IAsyncEnumerable<OrchestratorResponse> GetResponseStream(
        string message,
        string conversationId,
        List<ConversationMessage> priorMessages,
        bool enableThinking
    )
    {
        return priorMessages.Count > 0
            ? orchestrator.ProcessRequestStreamingWithHistoryAsync(
                message,
                conversationId,
                priorMessages,
                enableThinking
            )
            : orchestrator.ProcessRequestStreamingAsync(message, conversationId, enableThinking);
    }

    private async Task SaveAssistantMessageAsync(
        Guid threadId,
        ResponseCollector collector,
        CancellationToken cancellationToken
    )
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ThreadedChatHandler>>();

        logger.LogInformation(
            "Saving assistant message to thread {ThreadId}: ContentLength={ContentLength}, HasProjection={HasProjection}",
            threadId,
            collector.Content.Length,
            collector.ProjectionResult != null
        );

        var assistantMessage = new ChatMessage
        {
            ThreadId = threadId,
            Role = MessageRole.Assistant,
            Content = collector.Content,
            SubAgentName = collector.SubAgentName,
            MetadataJson =
                collector.ProjectionResult != null
                    ? JsonSerializer.Serialize(
                        new { projectionResult = collector.ProjectionResult }
                    )
                    : null,
        };

        await unitOfWork.Messages.AddAsync(assistantMessage, cancellationToken);

        logger.LogInformation(
            "Assistant message saved to thread {ThreadId} with ID {MessageId}",
            threadId,
            assistantMessage.Id
        );
    }

    private static string GenerateThreadTitle(string content)
    {
        var title = content.Length > 50 ? content[..50] + "..." : content;
        var firstSentenceEnd = title.IndexOfAny(['.', '?', '!', '\n']);

        if (firstSentenceEnd > 0 && firstSentenceEnd < title.Length - 3)
            title = title[..(firstSentenceEnd + 1)];

        return title.Trim();
    }

    private record ThreadContext(Guid ThreadId, List<ConversationMessage> PriorMessages);

    private class ResponseCollector
    {
        private readonly StringBuilder _content = new();

        public string? SubAgentName { get; private set; }
        public object? ProjectionResult { get; private set; }
        public string Content => _content.ToString();
        public bool HasContent => _content.Length > 0 || ProjectionResult != null;

        public void Collect(OrchestratorResponse response)
        {
            if (
                !string.IsNullOrEmpty(response.Content)
                && (response.Type == ResponseType.Content || response.Type == ResponseType.Progress)
            )
            {
                _content.Append(response.Content);
            }

            if (!string.IsNullOrEmpty(response.SubAgentName))
                SubAgentName = response.SubAgentName;

            if (response.ProjectionResult != null)
                ProjectionResult = response.ProjectionResult;
        }
    }
}
