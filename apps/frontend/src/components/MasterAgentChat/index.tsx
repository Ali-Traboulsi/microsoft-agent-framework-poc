import React, { useCallback, useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { ProjectionResult } from '../../interfaces/ProjectionResult.interface';
import {
  chatStreamMultiModal,
  chatStreamWithThread,
  connect,
  disconnect,
  onDelegationEvent,
  onWorkflowProgress,
  type DelegationEvent,
  type MasterStreamResponse,
  type WorkflowProgressEvent
} from '../../services/masterAgent';
import { ChatMessage, TelemetryData } from '../../services/masterAgent/types';
import { ChatThread, getThread, ThreadMessage } from '../../services/threads';
import { WorkflowStep } from '../Cards/WorkflowProgressCard';
import { UploadedFile } from '../FileUpload';
import { ChatMessages } from './components';
import { ChatInput } from './components/ChatInput';
import { ConnectionBanner } from './components/ConnectionBanner';
import { Sidebar } from './components/Sidebar';
import { WelcomeScreen } from './components/WelcomeScreen';
import {
  buildTelemetryData,
  createMessage,
  createTelemetryContext,
  prepareFileContents
} from './utils';

export const MasterAgentChat: React.FC = () => {
  // Core state
  const [input, setInput] = useState('');
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const [_telemetry, setTelemetry] = useState<TelemetryData>({});
  
  // File upload state
  const [uploadedFiles, setUploadedFiles] = useState<UploadedFile[]>([]);
  const [showFileUpload, setShowFileUpload] = useState(false);
  
  // UI state
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [enableThinking, setEnableThinking] = useState(false);
  const [showThreadList, setShowThreadList] = useState(true);
  
  // Thread state
  const [currentThreadId, setCurrentThreadId] = useState<string | null>(null);
  const [_selectedThread, setSelectedThread] = useState<ChatThread | null>(null);
  const [threadRefreshTrigger, setThreadRefreshTrigger] = useState(0);
  
  // Connection state
  const [isConnected, setIsConnected] = useState(false);
  const [isConnecting, setIsConnecting] = useState(false);
  
  // Refs
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const conversationId = useRef(`conv-${Date.now()}`);
  const workflowProgressMessageIdRef = useRef<string | null>(null);

  // Auto-scroll on new messages
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  // SignalR connection
  useEffect(() => {
    let cancelled = false;
    
    const establishConnection = async () => {
      setIsConnecting(true);
      try {
        await connect();
        if (!cancelled) {
          setConnectionError(null);
          setIsConnected(true);
        }
      } catch (error) {
        if (!cancelled) {
          console.error('SignalR connection error:', error);
          setConnectionError(error instanceof Error ? error.message : 'Failed to connect');
          setIsConnected(false);
        }
      } finally {
        if (!cancelled) setIsConnecting(false);
      }
    };
    
    establishConnection();

    return () => {
      cancelled = true;
      setTimeout(() => disconnect().catch(console.error), 100);
      setIsConnected(false);
    };
  }, []);

  // Handle workflow progress
  const handleWorkflowProgress = useCallback((chunk: MasterStreamResponse) => {
    if (!chunk.stepId || chunk.stepNumber === null || chunk.totalSteps === null) return;

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
        setMessages(prev => {
          const newWorkflowMsg = {
            id: newId,
            type: 'workflow-progress' as const,
            content: 'Processing...',
            timestamp: new Date(),
            workflowSteps: [newStep],
          };
          
          // Find the last user message - workflow progress should appear AFTER it
          let lastUserIndex = -1;
          for (let i = prev.length - 1; i >= 0; i--) {
            if (prev[i].type === 'user') {
              lastUserIndex = i;
              break;
            }
          }
          
          // Find agent message ONLY after the last user message (current request's response)
          let agentMsgIdx = -1;
          for (let i = prev.length - 1; i > lastUserIndex; i--) {
            if (prev[i].type === 'agent' || prev[i].type === 'multimodal') {
              agentMsgIdx = i;
              break;
            }
          }
          
          // Insert before agent message if one exists for current request
          if (agentMsgIdx >= 0 && agentMsgIdx > lastUserIndex) {
            return [...prev.slice(0, agentMsgIdx), newWorkflowMsg, ...prev.slice(agentMsgIdx)];
          }
          // Otherwise append at end (after user message)
          return [...prev, newWorkflowMsg];
        });
      } else {
        setMessages(prev => prev.map(msg => {
          if (msg.id !== workflowProgressMessageIdRef.current) return msg;
          
          const existingSteps = msg.workflowSteps || [];
          const stepIndex = existingSteps.findIndex(s => s.stepId === newStep.stepId);
          
          const updatedSteps = stepIndex >= 0
            ? existingSteps.map((s, i) => i === stepIndex ? newStep : s)
            : [...existingSteps, newStep];
          
          return { ...msg, workflowSteps: updatedSteps };
        }));
      }
    });
  }, []);

  // Register workflow progress handler
  useEffect(() => {
    if (!isConnected) return;

    const unsubscribe = onWorkflowProgress((event: WorkflowProgressEvent) => {
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

  // Register delegation event handler
  useEffect(() => {
    if (!isConnected) return;

    const unsubscribe = onDelegationEvent((_conversationId: string, event: DelegationEvent) => {
      const name = event.subAgentName || event.toolName || 'Unknown';
      const isToolType = event.type === 'ToolExecution' || event.type === 'ToolComplete';
      const isStartEvent = event.type === 'SubAgentDelegation' || event.type === 'ToolExecution';
      const isCompleteEvent = event.type === 'SubAgentComplete' || event.type === 'ToolComplete';
      
      flushSync(() => {
        setMessages(prev => {
          // Find the last user message index - we only want to add tool-calls AFTER this
          let lastUserIndex = -1;
          for (let i = prev.length - 1; i >= 0; i--) {
            if (prev[i].type === 'user') {
              lastUserIndex = i;
              break;
            }
          }
          
          // Find tool-calls message ONLY after the last user message (belongs to current request)
          let toolCallsIndex = -1;
          let lastAgentIndex = -1;
          
          for (let i = prev.length - 1; i > lastUserIndex; i--) {
            if (prev[i].type === 'tool-calls' && toolCallsIndex === -1) toolCallsIndex = i;
            if (prev[i].type === 'agent' && lastAgentIndex === -1) lastAgentIndex = i;
          }
          
          if (isStartEvent) {
            const newToolCall = {
              id: `tc-${name}-${Date.now()}`,
              name,
              type: (isToolType ? 'tool' : 'delegation') as 'tool' | 'delegation',
              status: 'running' as const,
              startTime: new Date(),
            };
            
            // Only append to existing tool-calls if it's AFTER the last user message
            if (toolCallsIndex >= 0 && toolCallsIndex > lastUserIndex) {
              const updated = [...prev];
              const existingMessage = { ...updated[toolCallsIndex] };
              existingMessage.toolCalls = [...(existingMessage.toolCalls || []), newToolCall];
              updated[toolCallsIndex] = existingMessage;
              return updated;
            } else {
              // Create new tool-calls message for this request
              const newMessage: ChatMessage = {
                id: `msg-tool-calls-${Date.now()}`,
                type: 'tool-calls',
                content: '',
                timestamp: new Date(),
                toolCalls: [newToolCall],
              };
              
              // Insert before agent message if one exists for current request
              if (lastAgentIndex >= 0 && lastAgentIndex > lastUserIndex) {
                const result = [...prev];
                result.splice(lastAgentIndex, 0, newMessage);
                return result;
              }
              // Otherwise just append after user message
              return [...prev, newMessage];
            }
          }
          
          // Only update tool-calls that belong to current request
          if (isCompleteEvent && toolCallsIndex >= 0 && toolCallsIndex > lastUserIndex) {
            const updated = [...prev];
            const existingMessage = { ...updated[toolCallsIndex] };
            existingMessage.toolCalls = (existingMessage.toolCalls || []).map(tc => {
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
  }, [isConnected]);

  // Add message utility
  const addMessage = useCallback((message: Omit<ChatMessage, 'id' | 'timestamp'>) => {
    setMessages(prev => [...prev, createMessage(message)]);
  }, []);

  // Convert thread messages
  const convertThreadMessagesToChatMessages = useCallback((threadMessages: ThreadMessage[]): ChatMessage[] => {
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
      
      if (projectionResult?.projectionId && !seenProjectionIds.has(projectionResult.projectionId)) {
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
    
    return result;
  }, []);

  // Thread selection handler
  const handleSelectThread = useCallback(async (thread: ChatThread | null) => {
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
      setSelectedThread(fullThread);
      setCurrentThreadId(fullThread.id);
      conversationId.current = fullThread.id;
      
      if (fullThread.messages?.length) {
        setMessages(convertThreadMessagesToChatMessages(fullThread.messages));
      } else {
        setMessages([]);
      }
      setTelemetry({});
    } catch (error) {
      console.error('Failed to load thread:', error);
      setConnectionError('Failed to load conversation');
    }
  }, [convertThreadMessagesToChatMessages]);

  // New chat handler
  const handleNewChat = useCallback(() => {
    setSelectedThread(null);
    setCurrentThreadId(null);
    setMessages([]);
    setTelemetry({});
    conversationId.current = `conv-${Date.now()}`;
  }, []);

  // Streaming chat handler
  const handleStreamingChat = async (userMessage: string) => {
    workflowProgressMessageIdRef.current = null;
    const ctx = createTelemetryContext();

    setIsStreaming(true);
    let thinkingMessageId: string | null = null;
    let contentMessageId: string | null = null;
    let accumulatedThinking = '';
    let accumulatedContent = '';

    try {
      for await (const chunk of chatStreamWithThread(
        userMessage, 
        currentThreadId,
        conversationId.current,
        enableThinking
      )) {
        if (chunk.type === 'ThreadCreated' && chunk.metadata?.threadId) {
          setCurrentThreadId(chunk.metadata.threadId as string);
          conversationId.current = chunk.metadata.threadId as string;
          setThreadRefreshTrigger(prev => prev + 1);
          continue;
        }
        
        if (chunk.isComplete) {
          setTelemetry(buildTelemetryData(ctx, chunk.metadata?.traceId as string));
          if (chunk.projectionResult) {
            addMessage({ type: 'projection', content: '', projectionResult: chunk.projectionResult });
          }
          break;
        }

        switch (chunk.type) {
          case 'Thinking':
            accumulatedThinking += chunk.content || '';
            if (!thinkingMessageId) {
              thinkingMessageId = `msg-${Date.now()}-${Math.random()}`;
              setMessages(prev => [...prev, { id: thinkingMessageId!, type: 'thinking', content: '🤔 ' + accumulatedThinking, timestamp: new Date() }]);
            } else {
              setMessages(prev => prev.map(msg => msg.id === thinkingMessageId ? { ...msg, content: '🤔 ' + accumulatedThinking } : msg));
            }
            break;

          case 'Reasoning':
            accumulatedThinking += chunk.content || '';
            if (!thinkingMessageId) {
              thinkingMessageId = `msg-${Date.now()}-${Math.random()}`;
              setMessages(prev => [...prev, { id: thinkingMessageId!, type: 'reasoning', content: accumulatedThinking, timestamp: new Date(), metadata: chunk.metadata || undefined }]);
            } else {
              setMessages(prev => prev.map(msg => msg.id === thinkingMessageId ? { ...msg, content: accumulatedThinking, type: 'reasoning' } : msg));
            }
            break;

          case 'SubAgentDelegation':
            if (chunk.subAgentName) ctx.delegationsUsed.push(chunk.subAgentName);
            break;

          case 'ToolExecution':
            if (chunk.toolName) ctx.toolsUsed.push(chunk.toolName);
            break;

          case 'StepStart':
          case 'StepComplete':
          case 'Progress':
            handleWorkflowProgress(chunk);
            break;

          case 'Content':
            accumulatedContent += chunk.content || '';
            if (!contentMessageId) {
              contentMessageId = `msg-${Date.now()}-${Math.random()}`;
              setMessages(prev => [...prev, { id: contentMessageId!, type: 'agent', content: accumulatedContent, timestamp: new Date() }]);
            } else {
              setMessages(prev => prev.map(msg => msg.id === contentMessageId ? { ...msg, content: accumulatedContent } : msg));
            }
            break;
        }
      }
    } catch (error: unknown) {
      const errorMsg = error instanceof Error ? error.message : 'Unknown error';
      addMessage({ type: 'agent', content: `❌ Error: ${errorMsg}` });
    } finally {
      setIsStreaming(false);
    }
  };

  // Multi-modal chat handler
  const handleMultiModalSend = async (userMessage: string, files: UploadedFile[]) => {
    const ctx = createTelemetryContext();
    setIsStreaming(true);
    let thinkingMessageId: string | null = null;
    let contentMessageId: string | null = null;
    let accumulatedThinking = '';
    let accumulatedContent = '';

    try {
      const fileContents = await prepareFileContents(files);
      const contents = [];
      if (userMessage.trim()) contents.push({ Type: 'text' as const, Text: userMessage });
      contents.push(...fileContents);

      for await (const chunk of chatStreamMultiModal(contents, conversationId.current, currentThreadId, enableThinking)) {
        if (chunk.isComplete) {
          setTelemetry(buildTelemetryData(ctx, chunk.metadata?.traceId as string));
          break;
        }

        switch (chunk.type) {
          case 'ThreadCreated':
            if (chunk.metadata?.threadId) {
              setCurrentThreadId(chunk.metadata.threadId as string);
              conversationId.current = chunk.metadata.threadId as string;
            }
            break;

          case 'Transcription':
            addMessage({ type: 'transcription', content: chunk.content || '', metadata: chunk.metadata || undefined });
            break;

          case 'Thinking':
            accumulatedThinking += chunk.content || '';
            if (!thinkingMessageId) {
              thinkingMessageId = `msg-${Date.now()}-${Math.random()}`;
              setMessages(prev => [...prev, { id: thinkingMessageId!, type: 'thinking', content: '🤔 ' + accumulatedThinking, timestamp: new Date() }]);
            } else {
              setMessages(prev => prev.map(msg => msg.id === thinkingMessageId ? { ...msg, content: '🤔 ' + accumulatedThinking } : msg));
            }
            break;

          case 'Reasoning':
            accumulatedThinking += chunk.content || '';
            if (!thinkingMessageId) {
              thinkingMessageId = `msg-${Date.now()}-${Math.random()}`;
              setMessages(prev => [...prev, { id: thinkingMessageId!, type: 'reasoning', content: accumulatedThinking, timestamp: new Date(), metadata: chunk.metadata || undefined }]);
            } else {
              setMessages(prev => prev.map(msg => msg.id === thinkingMessageId ? { ...msg, content: accumulatedThinking, type: 'reasoning' } : msg));
            }
            break;

          case 'SubAgentDelegation':
            if (chunk.subAgentName) ctx.delegationsUsed.push(chunk.subAgentName);
            break;

          case 'ToolCall':
            if (chunk.toolName) ctx.toolsUsed.push(chunk.toolName);
            break;

          case 'Content':
            accumulatedContent += chunk.content || '';
            if (!contentMessageId) {
              contentMessageId = `msg-${Date.now()}-${Math.random()}`;
              setMessages(prev => [...prev, { id: contentMessageId!, type: 'multimodal', content: accumulatedContent, timestamp: new Date() }]);
            } else {
              setMessages(prev => prev.map(msg => msg.id === contentMessageId ? { ...msg, content: accumulatedContent } : msg));
            }
            break;

          case 'Error':
            addMessage({ type: 'agent', content: `❌ Error: ${chunk.content}` });
            break;
        }
      }
    } catch (error: unknown) {
      const errorMsg = error instanceof Error ? error.message : 'Unknown error';
      addMessage({ type: 'agent', content: `❌ Error: ${errorMsg}` });
    } finally {
      setIsStreaming(false);
    }
  };

  // Send handler
  const handleSend = async () => {
    if ((!input.trim() && uploadedFiles.length === 0) || isStreaming) return;

    const userMessage = input.trim() || 'Analyze these files';
    const filesToSend = [...uploadedFiles];
    
    setInput('');
    setUploadedFiles([]);
    setShowFileUpload(false);
    
    addMessage({ type: 'user', content: userMessage, files: filesToSend });

    if (filesToSend.length > 0) {
      await handleMultiModalSend(userMessage, filesToSend);
    } else {
      await handleStreamingChat(userMessage);
    }
  };

  return (
    <div className="flex h-full bg-gray-50 dark:bg-gray-900">
      <Sidebar
        collapsed={sidebarCollapsed}
        onToggleCollapse={() => setSidebarCollapsed(!sidebarCollapsed)}
        showThreadList={showThreadList}
        onToggleView={setShowThreadList}
        currentThreadId={currentThreadId}
        onSelectThread={handleSelectThread}
        onNewChat={handleNewChat}
        threadRefreshTrigger={threadRefreshTrigger}
      />

      <div className="flex-1 flex flex-col bg-white dark:bg-gray-900">
        <ConnectionBanner
          isConnecting={isConnecting}
          connectionError={connectionError}
          onDismissError={() => setConnectionError(null)}
        />

        <div className="flex-1 overflow-y-auto">
          <div className="max-w-5xl mx-auto px-6 py-8 space-y-6">
            {messages.length === 0 ? (
              <WelcomeScreen onSuggestionClick={(text) => setInput(text)} />
            ) : (
              <ChatMessages messages={messages} isStreaming={isStreaming} />
            )}
            <div ref={messagesEndRef} />
          </div>
        </div>

        <ChatInput
          input={input}
          onInputChange={setInput}
          onSend={handleSend}
          isStreaming={isStreaming}
          enableThinking={enableThinking}
          onToggleThinking={() => setEnableThinking(!enableThinking)}
          showFileUpload={showFileUpload}
          onToggleFileUpload={() => setShowFileUpload(!showFileUpload)}
          uploadedFiles={uploadedFiles}
          onFilesChange={setUploadedFiles}
        />
      </div>
    </div>
  );
};
