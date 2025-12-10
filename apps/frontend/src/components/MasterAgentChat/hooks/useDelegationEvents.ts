import { useEffect } from 'react';
import { flushSync } from 'react-dom';
import { onDelegationEvent, type DelegationEvent } from '../../../services/masterAgent';
import type { ChatMessage } from '../../../services/masterAgent/types';

export function useDelegationEvents(
  isConnected: boolean,
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>
): void {
  useEffect(() => {
    if (!isConnected) return;

    console.log('🔗 Registering delegation event handler');
    const unsubscribe = onDelegationEvent((_conversationId: string, event: DelegationEvent) => {
      console.log('🎯 Delegation event received:', event.type, event.subAgentName || event.toolName);

      const name = event.subAgentName || event.toolName || 'Unknown';
      const isToolType = event.type === 'ToolExecution' || event.type === 'ToolComplete';
      const isStartEvent = event.type === 'SubAgentDelegation' || event.type === 'ToolExecution';
      const isCompleteEvent = event.type === 'SubAgentComplete' || event.type === 'ToolComplete';

      flushSync(() => {
        setMessages((prev) => {
          let toolCallsIndex = -1;
          let lastAgentIndex = -1;

          for (let i = prev.length - 1; i >= 0; i--) {
            if (prev[i].type === 'tool-calls' && toolCallsIndex === -1) {
              toolCallsIndex = i;
            }
            if (prev[i].type === 'agent' && lastAgentIndex === -1) {
              lastAgentIndex = i;
            }
          }

          if (isStartEvent) {
            const newToolCall = {
              id: `tc-${name}-${Date.now()}`,
              name,
              type: (isToolType ? 'tool' : 'delegation') as 'tool' | 'delegation',
              status: 'running' as const,
              startTime: new Date(),
            };

            if (toolCallsIndex >= 0) {
              const updated = [...prev];
              const existingMessage = { ...updated[toolCallsIndex] };
              existingMessage.toolCalls = [...(existingMessage.toolCalls || []), newToolCall];
              updated[toolCallsIndex] = existingMessage;
              return updated;
            } else {
              const newMessage: ChatMessage = {
                id: `msg-tool-calls-${Date.now()}`,
                type: 'tool-calls',
                content: '',
                timestamp: new Date(),
                toolCalls: [newToolCall],
              };

              if (lastAgentIndex >= 0) {
                const result = [...prev];
                result.splice(lastAgentIndex, 0, newMessage);
                return result;
              }
              return [...prev, newMessage];
            }
          }

          if (isCompleteEvent && toolCallsIndex >= 0) {
            const updated = [...prev];
            const existingMessage = { ...updated[toolCallsIndex] };
            existingMessage.toolCalls = (existingMessage.toolCalls || []).map((tc) => {
              if (tc.name === name && tc.status === 'running') {
                return { ...tc, status: 'completed' as const, endTime: new Date() };
              }
              return tc;
            });
            updated[toolCallsIndex] = existingMessage;
            return updated;
          }

          return prev;
        });
      });
    });

    return () => unsubscribe();
  }, [isConnected, setMessages]);
}
