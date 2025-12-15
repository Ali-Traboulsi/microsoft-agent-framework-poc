import {
  type MasterStreamResponse,
  chatStreamUnified,
} from '../../../services/masterAgent';
import type { ChatMessage, ContentInput, ProjectionResult } from '../../../services/masterAgent/types';
import type { UploadedFile } from '../../FileUpload';

export interface StreamingState {
  thinkingMessageId: string | null;
  contentMessageId: string | null;
  accumulatedThinking: string;
  accumulatedContent: string;
}

export interface StreamingHandlerContext {
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>;
  setIsStreaming: (streaming: boolean) => void;
  setTelemetry: React.Dispatch<React.SetStateAction<Record<string, unknown>>>;
  setCurrentThreadId: (id: string | null) => void;
  triggerThreadRefresh: () => void;
  conversationId: React.MutableRefObject<string>;
  currentThreadId: string | null;
  enableThinking: boolean;
  handleWorkflowProgress: (chunk: MasterStreamResponse) => void;
  resetWorkflowProgress: () => void;
  addMessage: (message: Omit<ChatMessage, 'id' | 'timestamp'>) => void;
}

function createStreamingState(): StreamingState {
  return {
    thinkingMessageId: null,
    contentMessageId: null,
    accumulatedThinking: '',
    accumulatedContent: '',
  };
}

function handleThinkingChunk(
  chunk: MasterStreamResponse,
  state: StreamingState,
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>
): void {
  state.accumulatedThinking += chunk.content || '';
  if (!state.thinkingMessageId) {
    const newId = `msg-${Date.now()}-${Math.random()}`;
    state.thinkingMessageId = newId;
    setMessages((prev) => [
      ...prev,
      {
        id: newId,
        type: 'thinking',
        content: '🤔 ' + state.accumulatedThinking,
        timestamp: new Date(),
      },
    ]);
  } else {
    setMessages((prev) =>
      prev.map((msg) =>
        msg.id === state.thinkingMessageId
          ? { ...msg, content: '🤔 ' + state.accumulatedThinking }
          : msg
      )
    );
  }
}

function handleReasoningChunk(
  chunk: MasterStreamResponse,
  state: StreamingState,
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>
): void {
  state.accumulatedThinking += chunk.content || '';
  if (!state.thinkingMessageId) {
    const newId = `msg-${Date.now()}-${Math.random()}`;
    state.thinkingMessageId = newId;
    setMessages((prev) => [
      ...prev,
      {
        id: newId,
        type: 'reasoning',
        content: state.accumulatedThinking,
        timestamp: new Date(),
        metadata: chunk.metadata || undefined,
      },
    ]);
  } else {
    setMessages((prev) =>
      prev.map((msg) =>
        msg.id === state.thinkingMessageId
          ? { ...msg, content: state.accumulatedThinking, type: 'reasoning' }
          : msg
      )
    );
  }
}

function handleContentChunk(
  chunk: MasterStreamResponse,
  state: StreamingState,
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>,
  messageType: 'agent' | 'multimodal' = 'agent'
): void {
  state.accumulatedContent += chunk.content || '';
  if (!state.contentMessageId) {
    const newId = `msg-${Date.now()}-${Math.random()}`;
    state.contentMessageId = newId;
    setMessages((prev) => {
      const newMsg: ChatMessage = {
        id: newId,
        type: messageType,
        content: state.accumulatedContent,
        timestamp: new Date(),
      };
      const workflowIdx = prev.findIndex((m) => m.type === 'workflow-progress');
      if (workflowIdx >= 0 && workflowIdx === prev.length - 1) {
        return [...prev, newMsg];
      }
      return [...prev, newMsg];
    });
  } else {
    setMessages((prev) =>
      prev.map((msg) =>
        msg.id === state.contentMessageId ? { ...msg, content: state.accumulatedContent } : msg
      )
    );
  }
}

export async function handleStreamingChat(
  userMessage: string,
  ctx: StreamingHandlerContext
): Promise<void> {
  ctx.resetWorkflowProgress();
  const requestStart = Date.now();
  const delegationsUsed: string[] = [];
  const toolsUsed: string[] = [];
  const state = createStreamingState();

  ctx.setIsStreaming(true);

  try {
    for await (const chunk of chatStreamUnified({
      message: userMessage,
      threadId: ctx.currentThreadId,
      conversationId: ctx.conversationId.current,
      enableThinking: ctx.enableThinking,
    })) {
      // Handle thread creation
      if (chunk.type === 'ThreadCreated' && chunk.metadata?.threadId) {
        const newThreadId = chunk.metadata.threadId as string;
        ctx.setCurrentThreadId(newThreadId);
        ctx.conversationId.current = newThreadId;
        ctx.triggerThreadRefresh();
        continue;
      }

      if (chunk.isComplete) {
        console.log('📊 Complete chunk received:', {
          hasProjectionResult: !!chunk.projectionResult,
        });

        const duration = Date.now() - requestStart;
        ctx.setTelemetry({
          duration,
          delegationCount: delegationsUsed.length,
          subAgentsUsed: [...new Set(delegationsUsed)],
          toolsUsed: [...new Set(toolsUsed)],
          traceId: chunk.metadata?.traceId as string | undefined,
        });

        if (chunk.projectionResult) {
          console.log('✅ Adding projection message');
          ctx.addMessage({
            type: 'projection',
            content: 'Projection Analysis Complete',
            projectionResult: chunk.projectionResult as ProjectionResult,
          });
        }
        break;
      }

      switch (chunk.type) {
        case 'Thinking':
          handleThinkingChunk(chunk, state, ctx.setMessages);
          break;

        case 'Reasoning':
          handleReasoningChunk(chunk, state, ctx.setMessages);
          break;

        case 'SubAgentDelegation':
          if (chunk.subAgentName) delegationsUsed.push(chunk.subAgentName);
          break;

        case 'ToolExecution':
          if (chunk.toolName) toolsUsed.push(chunk.toolName);
          break;

        case 'SubAgentComplete':
          break;

        case 'StepStart':
        case 'StepComplete':
        case 'Progress':
          ctx.handleWorkflowProgress(chunk);
          break;

        case 'Content':
          handleContentChunk(chunk, state, ctx.setMessages, 'agent');
          break;
      }
    }
  } catch (error: unknown) {
    console.error('Streaming error:', error);
    ctx.addMessage({
      type: 'agent',
      content: `❌ Error: ${error instanceof Error ? error.message : 'Unknown error'}`,
    });
  } finally {
    ctx.setIsStreaming(false);
  }
}

export async function handleMultiModalChat(
  userMessage: string,
  files: UploadedFile[],
  ctx: StreamingHandlerContext
): Promise<void> {
  const requestStart = Date.now();
  const delegationsUsed: string[] = [];
  const toolsUsed: string[] = [];
  const state = createStreamingState();

  ctx.setIsStreaming(true);

  try {
    // Convert files to base64
    const fileContents = await Promise.all(
      files.map(async (uploadedFile) => {
        const buffer = await uploadedFile.file.arrayBuffer();
        const bytes = new Uint8Array(buffer);

        let binary = '';
        const chunkSize = 8192;
        for (let i = 0; i < bytes.length; i += chunkSize) {
          const chunk = bytes.slice(i, i + chunkSize);
          binary += String.fromCharCode(...chunk);
        }
        const base64 = btoa(binary);
        const mediaType = uploadedFile.file.type || 'application/octet-stream';

        let contentType: 'image' | 'audio' | 'file' = 'file';
        if (mediaType.startsWith('image/')) contentType = 'image';
        else if (mediaType.startsWith('audio/')) contentType = 'audio';

        return {
          Type: contentType,
          Data: `data:${mediaType};base64,${base64}`,
          MediaType: mediaType,
          FileName: uploadedFile.file.name,
        };
      })
    );

    const contents: ContentInput[] = [];
    if (userMessage.trim()) {
      contents.push({ Type: 'text', Text: userMessage });
    }
    contents.push(...fileContents);

    for await (const chunk of chatStreamUnified({
      message: userMessage.trim() || undefined,
      contents,
      threadId: ctx.currentThreadId,
      conversationId: ctx.conversationId.current,
      enableThinking: ctx.enableThinking,
    })) {
      if (chunk.isComplete) {
        const duration = Date.now() - requestStart;
        ctx.setTelemetry({
          duration,
          delegationCount: delegationsUsed.length,
          subAgentsUsed: [...new Set(delegationsUsed)],
          toolsUsed: [...new Set(toolsUsed)],
        });
        break;
      }

      switch (chunk.type) {
        case 'ThreadCreated':
          if (chunk.metadata?.threadId) {
            ctx.setCurrentThreadId(chunk.metadata.threadId as string);
            ctx.conversationId.current = chunk.metadata.threadId as string;
          }
          break;

        case 'Transcription':
          ctx.addMessage({
            type: 'transcription',
            content: chunk.content || '',
            metadata: chunk.metadata || undefined,
          });
          break;

        case 'Thinking':
          handleThinkingChunk(chunk, state, ctx.setMessages);
          break;

        case 'Reasoning':
          handleReasoningChunk(chunk, state, ctx.setMessages);
          break;

        case 'SubAgentDelegation':
          if (chunk.subAgentName) delegationsUsed.push(chunk.subAgentName);
          break;

        case 'ToolCall':
          if (chunk.toolName) toolsUsed.push(chunk.toolName);
          break;

        case 'Content':
          handleContentChunk(chunk, state, ctx.setMessages, 'multimodal');
          break;

        case 'Error':
          ctx.addMessage({
            type: 'agent',
            content: `❌ Error: ${chunk.content}`,
          });
          break;
      }
    }
  } catch (error: unknown) {
    console.error('Multimodal streaming error:', error);
    ctx.addMessage({
      type: 'agent',
      content: `❌ Error: ${error instanceof Error ? error.message : 'Unknown error'}`,
    });
  } finally {
    ctx.setIsStreaming(false);
  }
}
