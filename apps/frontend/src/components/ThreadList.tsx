import { useCallback, useEffect, useState } from 'react';
import {
    ChatThread,
    createThread,
    deleteThread,
    getThreads,
} from '../services/threads';

interface ThreadListProps {
  selectedThreadId: string | null;
  onSelectThread: (thread: ChatThread | null) => void;
  onNewChat: () => void;
  refreshTrigger?: number;
}

export default function ThreadList({
  selectedThreadId,
  onSelectThread,
  onNewChat,
  refreshTrigger = 0,
}: ThreadListProps) {
  const [threads, setThreads] = useState<ChatThread[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const loadThreads = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await getThreads();
      setThreads(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load threads');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadThreads();
  }, [loadThreads, refreshTrigger]);

  const handleDelete = async (e: React.MouseEvent, threadId: string) => {
    e.stopPropagation();
    if (deletingId) return;

    try {
      setDeletingId(threadId);
      await deleteThread(threadId);
      setThreads((prev) => prev.filter((t) => t.id !== threadId));

      // If we deleted the selected thread, clear selection
      if (selectedThreadId === threadId) {
        onSelectThread(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete thread');
    } finally {
      setDeletingId(null);
    }
  };

  const handleNewChat = async () => {
    try {
      const thread = await createThread();
      setThreads((prev) => [thread, ...prev]);
      onSelectThread(thread);
      onNewChat();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create thread');
    }
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / (1000 * 60));
    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString();
  };

  return (
    <div className="flex flex-col h-full bg-gray-900 border-r border-gray-700">
      {/* Header */}
      <div className="p-4 border-b border-gray-700">
        <button
          onClick={handleNewChat}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors font-medium"
        >
          <svg
            className="w-5 h-5"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M12 4v16m8-8H4"
            />
          </svg>
          New Chat
        </button>
      </div>

      {/* Thread List */}
      <div className="flex-1 overflow-y-auto">
        {loading && threads.length === 0 && (
          <div className="flex items-center justify-center p-8 text-gray-400">
            <svg
              className="animate-spin h-6 w-6"
              fill="none"
              viewBox="0 0 24 24"
            >
              <circle
                className="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="4"
              />
              <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              />
            </svg>
          </div>
        )}

        {error && (
          <div className="p-4 text-red-400 text-sm">
            <p>{error}</p>
            <button
              onClick={loadThreads}
              className="mt-2 text-blue-400 hover:text-blue-300 underline"
            >
              Retry
            </button>
          </div>
        )}

        {!loading && !error && threads.length === 0 && (
          <div className="p-4 text-center text-gray-500">
            <p className="text-sm">No conversations yet</p>
            <p className="text-xs mt-1">Start a new chat to begin</p>
          </div>
        )}

        <div className="space-y-1 p-2">
          {threads.map((thread) => (
            <div
              key={thread.id}
              onClick={() => onSelectThread(thread)}
              className={`group relative flex items-center p-3 rounded-lg cursor-pointer transition-colors ${
                selectedThreadId === thread.id
                  ? 'bg-gray-700 text-white'
                  : 'hover:bg-gray-800 text-gray-300'
              }`}
            >
              {/* Chat icon */}
              <svg
                className="w-5 h-5 mr-3 flex-shrink-0 text-gray-500"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"
                />
              </svg>

              {/* Thread info */}
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium truncate">{thread.title}</p>
                <p className="text-xs text-gray-500 mt-0.5">
                  {formatDate(thread.updatedAt)} •{' '}
                  {thread.messageCount} message{thread.messageCount !== 1 ? 's' : ''}
                </p>
              </div>

              {/* Delete button */}
              <button
                onClick={(e) => handleDelete(e, thread.id)}
                disabled={deletingId === thread.id}
                className={`absolute right-2 p-1.5 rounded opacity-0 group-hover:opacity-100 transition-opacity ${
                  deletingId === thread.id
                    ? 'text-gray-500 cursor-not-allowed'
                    : 'text-gray-400 hover:text-red-400 hover:bg-gray-700'
                }`}
              >
                {deletingId === thread.id ? (
                  <svg
                    className="animate-spin h-4 w-4"
                    fill="none"
                    viewBox="0 0 24 24"
                  >
                    <circle
                      className="opacity-25"
                      cx="12"
                      cy="12"
                      r="10"
                      stroke="currentColor"
                      strokeWidth="4"
                    />
                    <path
                      className="opacity-75"
                      fill="currentColor"
                      d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                    />
                  </svg>
                ) : (
                  <svg
                    className="h-4 w-4"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
                    />
                  </svg>
                )}
              </button>
            </div>
          ))}
        </div>
      </div>

      {/* Footer */}
      <div className="p-4 border-t border-gray-700 text-center">
        <p className="text-xs text-gray-500">
          {threads.length} conversation{threads.length !== 1 ? 's' : ''}
        </p>
      </div>
    </div>
  );
}
