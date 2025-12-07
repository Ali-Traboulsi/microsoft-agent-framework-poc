using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Agents.AI;

namespace AgentFrameworkQuickStart.Services;

/// <summary>
/// Manages AgentThread instances for conversations with chat history persistence
/// </summary>
public class AgentThreadManager
{
    private readonly ConcurrentDictionary<string, AgentThread> _threads = new();
    private readonly ILogger<AgentThreadManager> _logger;

    public AgentThreadManager(ILogger<AgentThreadManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get or create an AgentThread for a conversation
    /// </summary>
    public AgentThread GetOrCreateThread(string conversationId, AIAgent agent)
    {
        return _threads.GetOrAdd(
            conversationId,
            _ =>
            {
                _logger.LogInformation(
                    "Creating new AgentThread for conversation {ConversationId}",
                    conversationId
                );
                return agent.GetNewThread();
            }
        );
    }

    /// <summary>
    /// Get an existing thread, returns null if not found
    /// </summary>
    public AgentThread? GetThread(string conversationId)
    {
        _threads.TryGetValue(conversationId, out var thread);
        return thread;
    }

    /// <summary>
    /// Serialize a thread to JSON for persistence
    /// </summary>
    public JsonElement SerializeThread(string conversationId)
    {
        if (_threads.TryGetValue(conversationId, out var thread))
        {
            var serialized = thread.Serialize();
            _logger.LogInformation(
                "Serialized thread for conversation {ConversationId}",
                conversationId
            );
            return serialized;
        }

        throw new KeyNotFoundException($"No thread found for conversation {conversationId}");
    }

    /// <summary>
    /// Deserialize and restore a thread from JSON
    /// </summary>
    public AgentThread DeserializeThread(
        string conversationId,
        JsonElement serializedState,
        AIAgent agent
    )
    {
        var thread = agent.DeserializeThread(serializedState);
        _threads[conversationId] = thread;
        _logger.LogInformation(
            "Deserialized and restored thread for conversation {ConversationId}",
            conversationId
        );
        return thread;
    }

    /// <summary>
    /// Clear a specific conversation thread
    /// </summary>
    public bool ClearThread(string conversationId)
    {
        var removed = _threads.TryRemove(conversationId, out _);
        if (removed)
        {
            _logger.LogInformation(
                "Cleared thread for conversation {ConversationId}",
                conversationId
            );
        }
        return removed;
    }

    /// <summary>
    /// Clear all threads (useful for testing or cleanup)
    /// </summary>
    public void ClearAllThreads()
    {
        var count = _threads.Count;
        _threads.Clear();
        _logger.LogInformation("Cleared all {Count} threads", count);
    }

    /// <summary>
    /// Get the number of active threads
    /// </summary>
    public int GetActiveThreadCount() => _threads.Count;

    /// <summary>
    /// Get all active conversation IDs
    /// </summary>
    public IEnumerable<string> GetActiveConversationIds() => _threads.Keys;
}
