import { useCallback, useEffect, useRef } from 'react';
import { flushSync } from 'react-dom';
import {
    onWorkflowProgress,
    type MasterStreamResponse,
    type WorkflowProgressEvent,
} from '../../../services/masterAgent';
import type { ChatMessage } from '../../../services/masterAgent/types';
import type { WorkflowStep } from '../../Cards/WorkflowProgressCard';
export interface WorkflowProgressHook {
  handleWorkflowProgress: (chunk: MasterStreamResponse) => void;
  resetWorkflowProgress: () => void;
}

export function useWorkflowProgress(
  isConnected: boolean,
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>
): WorkflowProgressHook {
  const workflowProgressMessageIdRef = useRef<string | null>(null);

  const handleWorkflowProgress = useCallback(
    (chunk: MasterStreamResponse) => {
      console.log('🎯 handleWorkflowProgress called with:', chunk);
      if (!chunk.stepId || chunk.stepNumber === null || chunk.totalSteps === null) {
        console.log('⚠️ Skipping chunk - missing required fields');
        return;
      }

      const newStep: WorkflowStep = {
        stepId: chunk.stepId,
        stepName: chunk.stepName || chunk.stepId,
        stepNameAr: chunk.stepNameAr || chunk.stepName || chunk.stepId,
        stepNumber: chunk.stepNumber,
        totalSteps: chunk.totalSteps,
        isCompleted: chunk.type === 'StepComplete',
        durationMs: chunk.stepDurationMs ?? undefined,
        details: chunk.stepDetails ?? undefined,
      };

      flushSync(() => {
        if (!workflowProgressMessageIdRef.current) {
          const newId = `msg-workflow-${Date.now()}`;
          workflowProgressMessageIdRef.current = newId;
          setMessages((prev) => {
            const newWorkflowMsg: ChatMessage = {
              id: newId,
              type: 'workflow-progress',
              content: 'Processing...',
              timestamp: new Date(),
              workflowSteps: [newStep],
            };

            const agentMsgIdx = prev.findIndex(
              (m) => m.type === 'agent' || m.type === 'multimodal'
            );
            if (agentMsgIdx >= 0) {
              return [
                ...prev.slice(0, agentMsgIdx),
                newWorkflowMsg,
                ...prev.slice(agentMsgIdx),
              ];
            }
            return [...prev, newWorkflowMsg];
          });
        } else {
          setMessages((prev) =>
            prev.map((msg) => {
              if (msg.id !== workflowProgressMessageIdRef.current) return msg;

              const existingSteps = msg.workflowSteps || [];
              const stepIndex = existingSteps.findIndex((s) => s.stepId === newStep.stepId);

              let updatedSteps: WorkflowStep[];
              if (stepIndex >= 0) {
                updatedSteps = [...existingSteps];
                updatedSteps[stepIndex] = newStep;
              } else {
                updatedSteps = [...existingSteps, newStep];
              }

              return { ...msg, workflowSteps: updatedSteps };
            })
          );
        }
      });
    },
    [setMessages]
  );

  const resetWorkflowProgress = useCallback(() => {
    workflowProgressMessageIdRef.current = null;
  }, []);

  // Register for real-time workflow progress events
  useEffect(() => {
    if (!isConnected) return;

    console.log('🔗 Registering workflow progress handler');
    const unsubscribe = onWorkflowProgress((event: WorkflowProgressEvent) => {
      console.log('📊 Workflow progress event received:', event);
      const chunk: MasterStreamResponse = {
        type: event.stepCompleted ? 'StepComplete' : 'StepStart',
        content: event.content ?? null,
        stepId: event.stepId,
        stepName: event.stepName ?? null,
        stepNameAr: event.stepNameAr ?? null,
        stepNumber: event.stepNumber,
        totalSteps: event.totalSteps,
        stepCompleted: event.stepCompleted,
        stepDurationMs: event.stepDurationMs ?? null,
        stepDetails: event.stepDetails ?? null,
        isComplete: false,
        subAgentName: null,
        toolName: null,
        metadata: null,
        projectionResult: null,
      };
      handleWorkflowProgress(chunk);
    });

    return () => unsubscribe();
  }, [isConnected, handleWorkflowProgress]);

  return {
    handleWorkflowProgress,
    resetWorkflowProgress,
  };
}
