import { useEffect } from 'react';
import { flushSync } from 'react-dom';
import { onDelegationEvent, type DelegationEvent } from '../../../services/masterAgent';
import type { ChatMessage } from '../../../services/masterAgent/types';

/**
 * Transparency event types that show what the agent is doing
 */
const TRANSPARENCY_EVENTS = [
  'ThinkingStart',
  'ThinkingProgress', 
  'ThinkingComplete',
  'MemoryRetrievalStart',
  'MemoryRetrievalComplete',
  'IntentAnalysisStart',
  'IntentAnalysisComplete',
  'ContextBuildingStart',
  'ContextBuildingComplete',
  'HistoryProcessing',
  'MemoryStorageStart',
  'MemoryStorageComplete',
  'ReasoningStep',
  'GeneratingResponse',
] as const;

type TransparencyEventType = typeof TRANSPARENCY_EVENTS[number];

function isTransparencyEvent(type: string): type is TransparencyEventType {
  return TRANSPARENCY_EVENTS.includes(type as TransparencyEventType);
}

/**
 * Get emoji and label for transparency events
 */
function getTransparencyEventDisplay(type: TransparencyEventType): { emoji: string; label: string } {
  const displays: Record<TransparencyEventType, { emoji: string; label: string }> = {
    ThinkingStart: { emoji: '🧠', label: 'Processing' },
    ThinkingProgress: { emoji: '💭', label: 'Thinking' },
    ThinkingComplete: { emoji: '✅', label: 'Complete' },
    MemoryRetrievalStart: { emoji: '📚', label: 'Loading Memory' },
    MemoryRetrievalComplete: { emoji: '✓', label: 'Memory Loaded' },
    IntentAnalysisStart: { emoji: '🔍', label: 'Analyzing Intent' },
    IntentAnalysisComplete: { emoji: '✓', label: 'Intent Analyzed' },
    ContextBuildingStart: { emoji: '🔧', label: 'Building Context' },
    ContextBuildingComplete: { emoji: '✓', label: 'Context Ready' },
    HistoryProcessing: { emoji: '📝', label: 'Processing History' },
    MemoryStorageStart: { emoji: '💾', label: 'Saving Memory' },
    MemoryStorageComplete: { emoji: '✓', label: 'Memory Saved' },
    ReasoningStep: { emoji: '🤔', label: 'Reasoning' },
    GeneratingResponse: { emoji: '🤖', label: 'Generating' },
  };
  return displays[type];
}

export function useDelegationEvents(
  isConnected: boolean,
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>
): void {
  useEffect(() => {
    if (!isConnected) return;

    console.log('🔗 Registering delegation event handler');
    const unsubscribe = onDelegationEvent((_conversationId: string, event: DelegationEvent) => {
      console.log('🎯 Delegation event received:', event.type, event.subAgentName || event.toolName || event.stepName);

      // Handle transparency events (thinking, memory, context building, etc.)
      if (isTransparencyEvent(event.type)) {
        const display = getTransparencyEventDisplay(event.type);
        
        flushSync(() => {
          setMessages((prev) => {
            // Find or create a thinking message
            let thinkingIndex = -1;
            for (let i = prev.length - 1; i >= 0; i--) {
              if (prev[i].type === 'thinking') {
                thinkingIndex = i;
                break;
              }
            }

            const stepInfo = {
              id: `step-${event.type}-${Date.now()}`,
              type: event.type,
              emoji: display.emoji,
              label: display.label,
              details: event.stepDetails || event.stepName || '',
              timestamp: new Date(),
              durationMs: event.stepDurationMs || undefined,
            };

            if (thinkingIndex >= 0) {
              // Append to existing thinking message
              const updated = [...prev];
              const existingMessage = { ...updated[thinkingIndex] };
              existingMessage.thinkingSteps = [...(existingMessage.thinkingSteps || []), stepInfo];
              updated[thinkingIndex] = existingMessage;
              return updated;
            } else {
              // Create new thinking message
              const newMessage: ChatMessage = {
                id: `msg-thinking-${Date.now()}`,
                type: 'thinking',
                content: '',
                timestamp: new Date(),
                thinkingSteps: [stepInfo],
              };
              return [...prev, newMessage];
            }
          });
        });
        return;
      }

      // Handle tool/sub-agent delegation events (existing logic)
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
