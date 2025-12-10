import { useCallback, useRef, useState } from 'react';
import type { ChatMessage, ProjectionResult } from '../../../services/masterAgent/types';
import { getThread, type ChatThread, type ThreadMessage } from '../../../services/threads';

export interface ThreadManagementHook {
  currentThreadId: string | null;
  conversationId: React.MutableRefObject<string>;
  threadRefreshTrigger: number;
  handleSelectThread: (thread: ChatThread | null) => Promise<void>;
  handleNewChat: () => void;
  setCurrentThreadId: (id: string | null) => void;
  triggerThreadRefresh: () => void;
}

export function useThreadManagement(
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>,
  setTelemetry: React.Dispatch<React.SetStateAction<Record<string, unknown>>>,
  setConnectionError: (error: string | null) => void
): ThreadManagementHook {
  const [currentThreadId, setCurrentThreadId] = useState<string | null>(null);
  const [_selectedThread, setSelectedThread] = useState<ChatThread | null>(null);
  const [threadRefreshTrigger, setThreadRefreshTrigger] = useState(0);
  const conversationId = useRef(`conv-${Date.now()}`);

  const convertThreadMessagesToChatMessages = useCallback(
    (threadMessages: ThreadMessage[]): ChatMessage[] => {
      const result: ChatMessage[] = [];
      const seenProjectionIds = new Set<string>();

      for (const msg of threadMessages) {
        const projectionResult = msg.metadata?.projectionResult as ProjectionResult | undefined;

        result.push({
          id: msg.id,
          type: msg.role.toLowerCase() === 'user' ? 'user' : 'agent',
          content: msg.content,
          timestamp: new Date(msg.timestamp),
          subAgentName: msg.subAgentName,
          metadata: msg.metadata,
          projectionResult: projectionResult,
        });

        if (projectionResult && projectionResult.projectionId) {
          if (!seenProjectionIds.has(projectionResult.projectionId)) {
            seenProjectionIds.add(projectionResult.projectionId);
            result.push({
              id: `${msg.id}-projection`,
              type: 'projection',
              content: '',
              timestamp: new Date(msg.timestamp),
              projectionResult: projectionResult,
            });
          }
        }
      }

      return result;
    },
    []
  );

  const handleSelectThread = useCallback(
    async (thread: ChatThread | null) => {
      if (!thread) {
        setSelectedThread(null);
        setCurrentThreadId(null);
        setMessages([]);
        setTelemetry({});
        conversationId.current = `conv-${Date.now()}`;
        return;
      }

      try {
        const fullThread = await getThread(thread.id);
        console.log('📥 Loaded thread:', fullThread.id, 'with', fullThread.messages?.length, 'messages');

        setSelectedThread(fullThread);
        setCurrentThreadId(fullThread.id);
        conversationId.current = fullThread.id;

        if (fullThread.messages && fullThread.messages.length > 0) {
          const chatMessages = convertThreadMessagesToChatMessages(fullThread.messages);
          console.log('📤 Converted to chat messages:', chatMessages.length, 'messages');
          setMessages(chatMessages);
        } else {
          setMessages([]);
        }

        setTelemetry({});
      } catch (error) {
        console.error('Failed to load thread:', error);
        setConnectionError('Failed to load conversation');
      }
    },
    [convertThreadMessagesToChatMessages, setMessages, setTelemetry, setConnectionError]
  );

  const handleNewChat = useCallback(() => {
    setSelectedThread(null);
    setCurrentThreadId(null);
    setMessages([]);
    setTelemetry({});
    conversationId.current = `conv-${Date.now()}`;
  }, [setMessages, setTelemetry]);

  const triggerThreadRefresh = useCallback(() => {
    setThreadRefreshTrigger((prev) => prev + 1);
  }, []);

  return {
    currentThreadId,
    conversationId,
    threadRefreshTrigger,
    handleSelectThread,
    handleNewChat,
    setCurrentThreadId,
    triggerThreadRefresh,
  };
}
