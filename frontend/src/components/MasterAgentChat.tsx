import React, { useEffect, useRef, useState } from 'react';
import { masterAgentService, ProjectionResult } from '../services/masterAgent';
import { FileUpload, UploadedFile } from './FileUpload';
import { MessageContent } from './MessageContent';
import { ProjectionResultCard } from './ProjectionResultCard';

interface ChatMessage {
  id: string;
  type: 'user' | 'agent' | 'thinking' | 'delegation' | 'tool' | 'telemetry' | 'multimodal' | 'transcription' | 'projection';
  content: string;
  timestamp: Date;
  subAgentName?: string;
  toolName?: string;
  metadata?: Record<string, any>;
  files?: UploadedFile[]; // For displaying user's uploaded files
  projectionResult?: ProjectionResult; // For structured projection data
}

interface TelemetryData {
  traceId?: string;
  duration?: number;
  delegationCount?: number;
  subAgentsUsed?: string[];
  toolsUsed?: string[];
}

export const MasterAgentChat: React.FC = () => {
  const [input, setInput] = useState('');
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const [telemetry, setTelemetry] = useState<TelemetryData>({});
  const [showTelemetry, setShowTelemetry] = useState(true);
  const [uploadedFiles, setUploadedFiles] = useState<UploadedFile[]>([]);
  const [showFileUpload, setShowFileUpload] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [enableThinking, setEnableThinking] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const conversationId = useRef(`conv-${Date.now()}`);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  useEffect(() => {
    masterAgentService.connect()
      .then(() => setConnectionError(null))
      .catch((error) => {
        console.error('SignalR connection error:', error);
        setConnectionError(error.message || 'Failed to connect to Master Agent');
      });

    return () => {
      masterAgentService.disconnect().catch(console.error);
    };
  }, []);

  const addMessage = (message: Omit<ChatMessage, 'id' | 'timestamp'>) => {
    setMessages(prev => [...prev, {
      ...message,
      id: `msg-${Date.now()}-${Math.random()}`,
      timestamp: new Date()
    }]);
  };

  const handleSend = async () => {
    if ((!input.trim() && uploadedFiles.length === 0) || isStreaming) return;

    const userMessage = input.trim() || 'Analyze these files';
    const filesToSend = [...uploadedFiles];
    
    setInput('');
    setUploadedFiles([]);
    setShowFileUpload(false);
    
    // Add user message with file attachments
    addMessage({ 
      type: 'user', 
      content: userMessage,
      files: filesToSend 
    });

    // If files are attached, use multimodal API
    if (filesToSend.length > 0) {
      await handleMultiModalSend(userMessage, filesToSend);
      return;
    }

    // Otherwise use regular streaming chat
    await handleStreamingChat(userMessage);
  };

  const handleMultiModalSend = async (userMessage: string, files: UploadedFile[]) => {
    // Use streaming approach for multimodal to get full master orchestration
    const requestStart = Date.now();
    const delegationsUsed: string[] = [];
    const toolsUsed: string[] = [];

    setIsStreaming(true);
    let thinkingMessageId: string | null = null;
    let accumulatedThinking = '';
    let contentMessageId: string | null = null;
    let accumulatedContent = '';

    try {
      // Convert files to base64 for streaming
      const fileDataPromises = files.map(async (uploadedFile) => {
        const buffer = await uploadedFile.file.arrayBuffer();
        const bytes = new Uint8Array(buffer);
        
        // Convert to base64 in chunks to avoid stack overflow
        let binary = '';
        const chunkSize = 8192;
        for (let i = 0; i < bytes.length; i += chunkSize) {
          const chunk = bytes.slice(i, i + chunkSize);
          binary += String.fromCharCode(...chunk);
        }
        const base64 = btoa(binary);
        
        const mediaType = uploadedFile.file.type || 'application/octet-stream';
        
        // Determine content type based on media type
        let contentType: 'image' | 'audio' | 'file' = 'file';
        if (mediaType.startsWith('image/')) {
          contentType = 'image';
        } else if (mediaType.startsWith('audio/')) {
          contentType = 'audio';
        }
        
        return {
          Type: contentType,
          Data: `data:${mediaType};base64,${base64}`,
          MediaType: mediaType,
          FileName: uploadedFile.file.name
        };
      });

      const fileContents = await Promise.all(fileDataPromises);

      // Add text content if message provided
      const contents = [];
      if (userMessage.trim()) {
        contents.push({
          Type: 'text' as const,
          Text: userMessage
        });
      }
      contents.push(...fileContents);

      // Use streaming multimodal endpoint
      for await (const chunk of masterAgentService.chatStreamMultiModal(contents, conversationId.current, enableThinking)) {
        if (chunk.isComplete) {
          const duration = Date.now() - requestStart;
          setTelemetry({
            duration,
            delegationCount: delegationsUsed.length,
            subAgentsUsed: [...new Set(delegationsUsed)],
            toolsUsed: [...new Set(toolsUsed)],
            traceId: chunk.metadata?.traceId
          });

          if (showTelemetry) {
            addMessage({
              type: 'telemetry',
              content: JSON.stringify({
                duration,
                delegations: delegationsUsed.length,
                subAgents: [...new Set(delegationsUsed)],
                tools: toolsUsed.length,
                traceId: chunk.metadata?.traceId?.substring(0, 8)
              }, null, 2)
            });
          }
          break;
        }

        switch (chunk.type) {
          case 'Transcription':
            // Display transcription in a special card before AI analysis
            addMessage({
              type: 'transcription',
              content: chunk.content || '',
              metadata: chunk.metadata || undefined
            });
            break;

          case 'Thinking':
            accumulatedThinking += chunk.content || '';
            if (!thinkingMessageId) {
              const newId = `msg-${Date.now()}-${Math.random()}`;
              thinkingMessageId = newId;
              setMessages(prev => [...prev, {
                id: newId,
                type: 'thinking',
                content: '🤔 ' + accumulatedThinking,
                timestamp: new Date()
              }]);
            } else {
              setMessages(prev => prev.map(msg =>
                msg.id === thinkingMessageId
                  ? { ...msg, content: '🤔 ' + accumulatedThinking }
                  : msg
              ));
            }
            break;

          case 'SubAgentDelegation':
            if (chunk.subAgentName) {
              delegationsUsed.push(chunk.subAgentName);
              addMessage({
                type: 'delegation',
                content: `Delegating to ${chunk.subAgentName}...`,
                subAgentName: chunk.subAgentName
              });
            }
            break;

          case 'ToolCall':
            if (chunk.toolName) {
              toolsUsed.push(chunk.toolName);
              addMessage({
                type: 'tool',
                content: chunk.content || `Using tool: ${chunk.toolName}`,
                toolName: chunk.toolName
              });
            }
            break;

          case 'Content':
            accumulatedContent += chunk.content || '';
            if (!contentMessageId) {
              const newId = `msg-${Date.now()}-${Math.random()}`;
              contentMessageId = newId;
              setMessages(prev => [...prev, {
                id: newId,
                type: 'multimodal',
                content: accumulatedContent,
                timestamp: new Date()
              }]);
            } else {
              setMessages(prev => prev.map(msg =>
                msg.id === contentMessageId
                  ? { ...msg, content: accumulatedContent }
                  : msg
              ));
            }
            break;

          case 'Error':
            addMessage({
              type: 'agent',
              content: `❌ Error: ${chunk.content}`
            });
            break;
        }
      }

    } catch (error: any) {
      console.error('Multimodal streaming error:', error);
      addMessage({
        type: 'agent',
        content: `❌ Error: ${error.message}`
      });
    } finally {
      setIsStreaming(false);
    }
  };

  const handleStreamingChat = async (userMessage: string) => {
    // Reset telemetry
    const requestStart = Date.now();
    const delegationsUsed: string[] = [];
    const toolsUsed: string[] = [];

    setIsStreaming(true);
    let thinkingMessageId: string | null = null;
    let contentMessageId: string | null = null;
    let accumulatedThinking = '';
    let accumulatedContent = '';

    // Check if this might be a projection request
    const projectionKeywords = ['project', 'profit', 'return', 'invest', 'earning', 'calculate', 'estimate', 'forecast'];
    const mightBeProjection = projectionKeywords.some(kw => userMessage.toLowerCase().includes(kw));

    try {
      // If it might be a projection request, use REST API to get structured result
      if (mightBeProjection) {
        const response = await masterAgentService.chat(userMessage, conversationId.current, enableThinking);
        
        const duration = Date.now() - requestStart;
        setTelemetry({
          duration,
          delegationCount: response.data.subAgentsUsed.length,
          subAgentsUsed: response.data.subAgentsUsed,
        });

        // Add agent response
        addMessage({
          type: 'agent',
          content: response.data.response,
        });

        // If we have projection data, add it as a separate visual card
        if (response.data.hasProjectionResult && response.data.projectionResult) {
          addMessage({
            type: 'projection',
            content: 'Projection Analysis Complete',
            projectionResult: response.data.projectionResult,
          });
        }

        // Add telemetry if enabled
        if (showTelemetry) {
          addMessage({
            type: 'telemetry',
            content: JSON.stringify({
              duration,
              delegations: response.data.subAgentsUsed.length,
              subAgents: response.data.subAgentsUsed,
              hasProjection: response.data.hasProjectionResult,
            }, null, 2)
          });
        }
        
        return;
      }

      // Otherwise use streaming for regular chat
      for await (const chunk of masterAgentService.chatStream(userMessage, conversationId.current, enableThinking)) {
        if (chunk.isComplete) {
          // Final telemetry update
          const duration = Date.now() - requestStart;
          setTelemetry({
            duration,
            delegationCount: delegationsUsed.length,
            subAgentsUsed: [...new Set(delegationsUsed)],
            toolsUsed: [...new Set(toolsUsed)],
            traceId: chunk.metadata?.traceId
          });
          
          // Add telemetry message
          if (showTelemetry) {
            addMessage({
              type: 'telemetry',
              content: JSON.stringify({
                duration,
                delegations: delegationsUsed.length,
                subAgents: [...new Set(delegationsUsed)],
                tools: toolsUsed.length,
                traceId: chunk.metadata?.traceId?.substring(0, 8)
              }, null, 2)
            });
          }
          break;
        }

        switch (chunk.type) {
          case 'Thinking':
            // Accumulate thinking chunks
            accumulatedThinking += chunk.content || '';
            if (!thinkingMessageId) {
              // Create initial thinking message
              const newId = `msg-${Date.now()}-${Math.random()}`;
              thinkingMessageId = newId;
              setMessages(prev => [...prev, {
                id: newId,
                type: 'thinking',
                content: '🤔 ' + accumulatedThinking,
                timestamp: new Date()
              }]);
            } else {
              // Update existing thinking message
              setMessages(prev => prev.map(msg => 
                msg.id === thinkingMessageId 
                  ? { ...msg, content: '🤔 ' + accumulatedThinking }
                  : msg
              ));
            }
            break;

          case 'SubAgentDelegation':
            // Show delegation
            if (chunk.subAgentName) {
              delegationsUsed.push(chunk.subAgentName);
              addMessage({
                type: 'delegation',
                content: `Delegating to ${chunk.subAgentName}...`,
                subAgentName: chunk.subAgentName
              });
            }
            break;

          case 'ToolExecution':
            // Show tool execution - but skip "completed" messages to avoid duplicates
            if (chunk.toolName && !chunk.metadata?.hideInUI) {
              toolsUsed.push(chunk.toolName);
              addMessage({
                type: 'tool',
                content: `Executing: ${chunk.toolName}`,
                toolName: chunk.toolName
              });
            }
            break;

          case 'SubAgentComplete':
            // Sub-agent finished
            addMessage({
              type: 'delegation',
              content: `✅ ${chunk.subAgentName} completed`,
              subAgentName: chunk.subAgentName!
            });
            break;

          case 'Content':
            // Accumulate content chunks
            accumulatedContent += chunk.content || '';
            if (!contentMessageId) {
              // Create initial content message
              const newId = `msg-${Date.now()}-${Math.random()}`;
              contentMessageId = newId;
              setMessages(prev => [...prev, {
                id: newId,
                type: 'agent',
                content: accumulatedContent,
                timestamp: new Date()
              }]);
            } else {
              // Update existing content message
              setMessages(prev => prev.map(msg => 
                msg.id === contentMessageId 
                  ? { ...msg, content: accumulatedContent }
                  : msg
              ));
            }
            break;
        }
      }

    } catch (error: any) {
      console.error('Streaming error:', error);
      addMessage({
        type: 'agent',
        content: `❌ Error: ${error.message}`
      });
    } finally {
      setIsStreaming(false);
    }
  };

  const handleClearConversation = async () => {
    if (!confirm('Clear conversation history? This will start a new conversation.')) {
      return;
    }

    try {
      await masterAgentService.clearConversation(conversationId.current);
      
      // Generate new conversation ID
      conversationId.current = `conv-${Date.now()}`;
      
      // Clear messages
      setMessages([]);
      
      // Reset telemetry
      setTelemetry({});
      
      console.log('✅ Conversation cleared, new ID:', conversationId.current);
    } catch (error) {
      console.error('Failed to clear conversation:', error);
      addMessage({
        type: 'agent',
        content: '❌ Failed to clear conversation history'
      });
    }
  };

  const handleNewConversation = () => {
    // Generate new conversation ID without clearing server-side
    conversationId.current = `conv-${Date.now()}`;
    setMessages([]);
    setTelemetry({});
    console.log('🆕 Started new conversation:', conversationId.current);
  };

  const getMessageBubbleStyle = (type: ChatMessage['type'], toolName?: string): string => {
    if (type === 'user') {
      return 'bg-gradient-to-br from-blue-500 to-purple-600 text-white px-6 py-4 rounded-3xl shadow-lg';
    }
    if (type === 'transcription') {
      return 'bg-gradient-to-br from-purple-50 to-pink-50 dark:from-purple-900/40 dark:to-pink-900/40 border-2 border-purple-300 dark:border-purple-700 text-gray-900 dark:text-purple-100 px-6 py-4 rounded-3xl shadow-md';
    }
    if (type === 'delegation' && toolName === 'SearchWeb') {
      return 'bg-gradient-to-br from-teal-50 to-cyan-50 dark:from-teal-900/40 dark:to-cyan-900/40 border-2 border-teal-200 dark:border-teal-700 text-gray-900 dark:text-teal-100 px-6 py-4 rounded-3xl shadow-md';
    }
    if (type === 'delegation') {
      return 'bg-gradient-to-br from-purple-50 to-pink-50 dark:from-purple-900/40 dark:to-pink-900/40 border-2 border-purple-200 dark:border-purple-700 text-gray-900 dark:text-purple-100 px-6 py-4 rounded-3xl shadow-md';
    }
    if (type === 'tool') {
      return 'bg-gradient-to-br from-blue-50 to-indigo-50 dark:from-blue-900/40 dark:to-indigo-900/40 border-2 border-blue-200 dark:border-blue-700 text-gray-900 dark:text-blue-100 px-6 py-4 rounded-3xl shadow-md';
    }
    if (type === 'multimodal') {
      return 'bg-gradient-to-br from-pink-50 via-purple-50 to-indigo-50 dark:from-pink-900/40 dark:via-purple-900/40 dark:to-indigo-900/40 border-2 border-purple-300 dark:border-purple-700 text-gray-900 dark:text-purple-100 px-6 py-4 rounded-3xl shadow-lg';
    }
    if (type === 'thinking') {
      return 'bg-gradient-to-br from-yellow-50 to-amber-50 dark:from-yellow-900/40 dark:to-amber-900/40 border-2 border-yellow-200 dark:border-yellow-700 text-gray-900 dark:text-yellow-100 px-6 py-4 rounded-3xl shadow-md';
    }
    if (type === 'agent') {
      return 'bg-gradient-to-br from-gray-50 to-slate-50 dark:from-gray-800/40 dark:to-slate-800/40 border-2 border-gray-200 dark:border-gray-700 text-gray-900 dark:text-gray-100 px-6 py-4 rounded-3xl shadow-md';
    }
    if (type === 'telemetry') {
      return 'bg-gray-900 text-gray-100 font-mono text-xs px-6 py-4 rounded-3xl shadow-lg';
    }
    return 'bg-white dark:bg-gray-800 border-2 border-gray-200 dark:border-gray-700 text-gray-900 dark:text-gray-100 px-6 py-4 rounded-3xl shadow-md';
  };

  const getMessageIcon = (type: ChatMessage['type'], toolName?: string) => {
    switch (type) {
      case 'transcription':
        return '🎤';
      case 'multimodal':
        return '🎨';
      case 'thinking':
        return '🤔';
      case 'delegation':
        return '🔄';
      case 'tool':
        // Show globe icon for web search, otherwise wrench
        return toolName === 'SearchWeb' ? '🌐' : '🔧';
      case 'telemetry':
        return '📊';
      default:
        return null;
    }
  };

  return (
    <div className="flex h-full bg-gray-50 dark:bg-gray-900">
      {/* Sidebar */}
      <div className={`${sidebarCollapsed ? 'w-16' : 'w-80'} bg-gradient-to-b from-slate-900 via-slate-800 to-slate-900 text-white flex flex-col transition-all duration-300 border-r border-slate-700 shadow-2xl`}>
        {/* Sidebar Header */}
        <div className="p-6 border-b border-slate-700">
          <div className="flex items-center justify-between mb-4">
            {!sidebarCollapsed && (
              <div className="flex items-center gap-3">
                <div className="w-12 h-12 bg-gradient-to-br from-blue-500 to-purple-600 rounded-xl flex items-center justify-center shadow-lg">
                  <span className="text-2xl">🤖</span>
                </div>
                <div>
                  <h1 className="text-lg font-bold">Master Agent</h1>
                  <p className="text-xs text-slate-400">AI Orchestrator</p>
                </div>
              </div>
            )}
            <button
              onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
              className="p-2 hover:bg-slate-700 rounded-lg transition-colors"
              title={sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            >
              <span className="text-xl">{sidebarCollapsed ? '☰' : '‹'}</span>
            </button>
          </div>
          
          {!sidebarCollapsed && (
            <div className="space-y-2">
              <label className="flex items-center gap-2 text-sm cursor-pointer bg-slate-800/50 p-2 rounded-lg hover:bg-slate-700/50 transition-colors">
                <input
                  type="checkbox"
                  checked={showTelemetry}
                  onChange={(e) => setShowTelemetry(e.target.checked)}
                  className="rounded"
                />
                <span>Show Telemetry</span>
              </label>

              {/* New Conversation Button */}
              <button
                onClick={handleNewConversation}
                disabled={isStreaming}
                className="w-full flex items-center gap-2 text-sm bg-blue-600 hover:bg-blue-700 disabled:bg-slate-700 disabled:opacity-50 p-2 rounded-lg transition-colors"
                title="Start a new conversation"
              >
                <span>➕</span>
                <span>New Chat</span>
              </button>

              {/* Clear Conversation Button */}
              {messages.length > 0 && (
                <button
                  onClick={handleClearConversation}
                  disabled={isStreaming}
                  className="w-full flex items-center gap-2 text-sm bg-red-600 hover:bg-red-700 disabled:bg-slate-700 disabled:opacity-50 p-2 rounded-lg transition-colors"
                  title="Clear conversation history on server"
                >
                  <span>🗑️</span>
                  <span>Clear History</span>
                </button>
              )}
            </div>
          )}
        </div>

        {/* Connection Status */}
        {!sidebarCollapsed && (
          <div className="px-6 py-4 border-b border-slate-700">
            <div className="flex items-center gap-2 text-sm">
              <div className={`w-2 h-2 rounded-full ${connectionError ? 'bg-red-500 animate-pulse' : 'bg-green-500'}`} />
              <span className="text-slate-300">
                {connectionError ? 'Disconnected' : 'Connected'}
              </span>
            </div>
          </div>
        )}

        {/* Telemetry Stats */}
        {!sidebarCollapsed && telemetry.duration && (
          <div className="px-6 py-4 border-b border-slate-700">
            <div className="space-y-3">
              <h3 className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Session Stats</h3>
              <div className="space-y-2 text-sm">
                <div className="flex items-center justify-between bg-slate-800/30 p-2 rounded">
                  <span className="text-slate-400">⏱️ Duration</span>
                  <span className="font-semibold">{telemetry.duration}ms</span>
                </div>
                <div className="flex items-center justify-between bg-slate-800/30 p-2 rounded">
                  <span className="text-slate-400">🔄 Delegations</span>
                  <span className="font-semibold">{telemetry.delegationCount || 0}</span>
                </div>
                <div className="flex items-center justify-between bg-slate-800/30 p-2 rounded">
                  <span className="text-slate-400">🔧 Tools</span>
                  <span className="font-semibold">{telemetry.toolsUsed?.length || 0}</span>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Sub-Agents Info */}
        {!sidebarCollapsed && (
          <div className="flex-1 overflow-y-auto px-6 py-4">
            <h3 className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-3">Available Agents</h3>
            <div className="space-y-2">
              {[
                { icon: '📊', name: 'Investment Advisor', desc: 'Fund research' },
                { icon: '💼', name: 'Portfolio Manager', desc: 'Portfolio mgmt' },
                { icon: '🏦', name: 'Account Services', desc: 'Account ops' },
                { icon: '⚖️', name: 'Compliance Officer', desc: 'Risk & compliance' }
              ].map((agent) => (
                <div key={agent.name} className="bg-slate-800/40 p-3 rounded-lg hover:bg-slate-800/60 transition-colors">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="text-lg">{agent.icon}</span>
                    <span className="font-medium text-xs">{agent.name}</span>
                  </div>
                  <p className="text-xs text-slate-400">{agent.desc}</p>
                </div>
              ))}
            </div>

            <div className="mt-6 p-3 bg-teal-900/30 border border-teal-700/30 rounded-lg">
              <div className="flex items-center gap-2 mb-2">
                <span className="text-lg">🌐</span>
                <span className="font-semibold text-xs">Web Search</span>
              </div>
              <p className="text-xs text-slate-400">Real-time web information</p>
            </div>
          </div>
        )}

        {/* Sidebar Footer */}
        {!sidebarCollapsed && (
          <div className="p-4 border-t border-slate-700 space-y-2">
            <div className="text-xs">
              <div className="text-slate-400 mb-1">Conversation ID</div>
              <div className="font-mono text-slate-300 bg-slate-800/50 px-2 py-1 rounded text-[10px] break-all">
                {conversationId.current}
              </div>
            </div>
            <div className="flex items-center gap-2 text-xs text-slate-500">
              <div className="w-2 h-2 rounded-full bg-green-500"></div>
              <span>History tracked on server</span>
            </div>
          </div>
        )}
      </div>

      {/* Main Chat Area */}
      <div className="flex-1 flex flex-col bg-white dark:bg-gray-900">
        {/* Connection Error Banner */}
        {connectionError && (
          <div className="bg-red-50 dark:bg-red-900/30 border-b border-red-200 dark:border-red-800 p-3 animate-slideDown">
            <div className="flex items-start gap-2 max-w-5xl mx-auto">
              <span className="text-red-600 dark:text-red-400 text-xl">⚠️</span>
              <div className="flex-1">
                <p className="text-red-800 dark:text-red-300 font-medium text-sm">Connection Error</p>
                <p className="text-red-600 dark:text-red-400 text-xs mt-1">{connectionError}</p>
                <p className="text-red-600 dark:text-red-400 text-xs mt-1">
                  Make sure the backend is running: <code className="bg-red-100 dark:bg-red-800/40 px-1 py-0.5 rounded">cd src && dotnet run</code>
                </p>
              </div>
              <button
                onClick={() => setConnectionError(null)}
                className="text-red-600 dark:text-red-400 hover:text-red-800 dark:hover:text-red-300"
              >
                ✕
              </button>
            </div>
          </div>
        )}

        {/* Messages Container */}
        <div className="flex-1 overflow-y-auto">
          <div className="max-w-5xl mx-auto px-6 py-8 space-y-6">
            {messages.length === 0 && (
              <div className="text-center text-gray-500 dark:text-gray-400 mt-20">
                <div className="inline-block p-6 bg-gradient-to-br from-blue-50 to-purple-50 dark:from-blue-900/40 dark:to-purple-900/40 rounded-3xl shadow-lg mb-6">
                  <span className="text-6xl">🤖</span>
                </div>
                <h2 className="text-3xl font-bold text-gray-800 dark:text-gray-100 mb-3">Welcome to Master Agent</h2>
                <p className="text-lg text-gray-600 dark:text-gray-300 mb-8">
                  I coordinate multiple specialized AI agents to handle your requests
                </p>
                
                <div className="grid grid-cols-2 gap-4 max-w-3xl mx-auto mb-8">
                  {[
                    'Analyze my investment portfolio',
                    'Search for latest AI trends',
                    'Show account balances',
                    'Recommend mutual funds'
                  ].map((example, idx) => (
                    <button
                      key={idx}
                      onClick={() => setInput(example)}
                      className="bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 border-2 border-gray-200 dark:border-gray-600 hover:border-blue-300 dark:hover:border-blue-500 text-gray-700 dark:text-gray-200 p-4 rounded-xl transition-all text-left shadow-sm hover:shadow-md"
                    >
                      <span className="text-2xl mb-2 block">{['💼', '🌐', '🏦', '📊'][idx]}</span>
                      <span className="text-sm font-medium">{example}</span>
                    </button>
                  ))}
                </div>

                <div className="bg-gradient-to-r from-teal-50 to-cyan-50 dark:from-teal-900/40 dark:to-cyan-900/40 border border-teal-200 dark:border-teal-700 rounded-xl p-6 max-w-3xl mx-auto">
                  <div className="flex items-center gap-3 mb-3">
                    <span className="text-3xl">🎨</span>
                    <div className="text-left">
                      <h3 className="font-bold text-gray-800 dark:text-gray-100">Multi-Modal Support</h3>
                      <p className="text-sm text-gray-600 dark:text-gray-300">Upload images, audio, and documents</p>
                    </div>
                  </div>
                  <div className="grid grid-cols-3 gap-2 text-xs">
                    <div className="bg-white/60 dark:bg-gray-800/60 p-2 rounded text-gray-800 dark:text-gray-200">🖼️ Images</div>
                    <div className="bg-white/60 dark:bg-gray-800/60 p-2 rounded text-gray-800 dark:text-gray-200">🎵 Audio</div>
                    <div className="bg-white/60 dark:bg-gray-800/60 p-2 rounded text-gray-800 dark:text-gray-200">📄 Documents</div>
                  </div>
                </div>
              </div>
            )}

            {messages.map((msg) => (
              <div
                key={msg.id}
                className={`flex ${msg.type === 'user' ? 'justify-end' : 'justify-start'}`}
              >
                {/* Special rendering for projection results */}
                {msg.type === 'projection' && msg.projectionResult ? (
                  <div className="w-full max-w-5xl mx-auto">
                    <ProjectionResultCard result={msg.projectionResult} />
                  </div>
                ) : (
                <div className={`max-w-4xl ${msg.type === 'user' ? 'ml-auto' : 'mr-auto'} ${getMessageBubbleStyle(msg.type, msg.toolName)}`}>
                  {msg.type !== 'user' && getMessageIcon(msg.type, msg.toolName) && (
                    <div className="flex items-center gap-2 mb-2">
                      <span className="text-xl">{getMessageIcon(msg.type, msg.toolName)}</span>
                      <span className="text-xs font-semibold uppercase tracking-wide opacity-70">
                        {msg.type === 'transcription' ? `Audio Transcription${msg.metadata?.FileName ? ` - ${msg.metadata.FileName}` : ''}` :
                         msg.type === 'multimodal' ? 'Multi-Modal Response' : 
                         msg.type === 'thinking' ? 'Thinking' :
                         msg.type === 'delegation' ? msg.subAgentName :
                         msg.type === 'tool' ? msg.toolName : 
                         msg.type}
                      </span>
                    </div>
                  )}
                  
                  {/* Show attached files for user messages */}
                  {msg.files && msg.files.length > 0 && (
                    <div className="mb-3 grid grid-cols-2 gap-2">
                      {msg.files.map((file) => (
                        <div key={file.id} className="flex items-center gap-2 bg-white/80 backdrop-blur rounded-lg p-2 text-xs border border-white/20">
                          {file.preview ? (
                            <img src={file.preview} alt={file.file.name} className="w-12 h-12 object-cover rounded" />
                          ) : (
                            <span className="text-2xl">{file.file.type.startsWith('audio/') ? '🎵' : '📄'}</span>
                          )}
                          <div className="flex-1 truncate">
                            <div className="font-medium">{file.file.name}</div>
                            <div className="text-gray-600">{(file.file.size / 1024).toFixed(1)} KB</div>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                  
                  {msg.type === 'telemetry' ? (
                    <pre className="text-xs bg-gray-900 text-gray-100 p-3 rounded-lg overflow-x-auto">{msg.content}</pre>
                  ) : (
                    <MessageContent 
                      content={msg.content} 
                      isAgent={msg.type !== 'user'} 
                    />
                  )}
                  
                  <div className="flex items-center justify-between mt-2 text-xs opacity-60">
                    <span>{msg.timestamp.toLocaleTimeString()}</span>
                    {msg.metadata?.contentTypes && (
                      <div className="flex gap-1">
                        {msg.metadata.contentTypes.map((type: string) => (
                          <span key={type} className="px-2 py-0.5 bg-white/20 rounded-full">
                            {type}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
                )}
              </div>
            ))}

            {isStreaming && (
              <div className="flex justify-start">
                <div className="bg-gray-100 dark:bg-gray-800 px-6 py-4 rounded-2xl shadow-sm">
                  <div className="flex gap-2">
                    <div className="w-3 h-3 bg-blue-500 rounded-full animate-bounce" />
                    <div className="w-3 h-3 bg-purple-500 rounded-full animate-bounce" style={{ animationDelay: '0.1s' }} />
                    <div className="w-3 h-3 bg-pink-500 rounded-full animate-bounce" style={{ animationDelay: '0.2s' }} />
                  </div>
                </div>
              </div>
            )}

            <div ref={messagesEndRef} />
          </div>
        </div>

        {/* Input Area */}
        <div className="border-t dark:border-gray-700 bg-white dark:bg-gray-900 shadow-2xl">
          {/* File Upload Panel */}
          {showFileUpload && (
            <div className="border-b dark:border-gray-700 bg-gray-50 dark:bg-gray-800 px-6 py-4 animate-slideDown max-w-5xl mx-auto">
              <div className="flex items-center justify-between mb-3">
                <h3 className="font-semibold text-gray-900 dark:text-gray-100 flex items-center gap-2">
                  <span className="text-xl">📎</span>
                  Attach Files
                </h3>
                <button
                  onClick={() => {
                    setShowFileUpload(false);
                    setUploadedFiles([]);
                  }}
                  className="text-gray-400 dark:text-gray-500 hover:text-gray-600 dark:hover:text-gray-300 transition-colors"
                >
                  ✕
                </button>
              </div>
              <FileUpload
                files={uploadedFiles}
                onFilesChange={setUploadedFiles}
                maxFiles={5}
              />
            </div>
          )}

          <div className="max-w-5xl mx-auto px-6 py-4">
            {/* File Chips */}
            {uploadedFiles.length > 0 && !showFileUpload && (
              <div className="mb-3 flex flex-wrap gap-2">
                {uploadedFiles.map((file) => (
                  <div
                    key={file.id}
                    className="flex items-center gap-2 bg-gradient-to-r from-blue-50 to-purple-50 dark:from-blue-900/40 dark:to-purple-900/40 border border-blue-200 dark:border-blue-700 rounded-full px-4 py-2 text-sm group hover:shadow-md transition-all"
                  >
                    <span className="text-lg">{file.file.type.startsWith('image/') ? '🖼️' : file.file.type.startsWith('audio/') ? '🎵' : '📄'}</span>
                    <span className="max-w-[200px] truncate font-medium text-gray-900 dark:text-gray-100">{file.file.name}</span>
                    <button
                      onClick={() => setUploadedFiles(uploadedFiles.filter(f => f.id !== file.id))}
                      className="text-blue-600 dark:text-blue-400 hover:text-red-600 dark:hover:text-red-400 transition-colors font-bold"
                    >
                      ✕
                    </button>
                  </div>
                ))}
              </div>
            )}

            <div className="flex gap-3">
              {/* Thinking Mode Toggle */}
              <button
                onClick={() => setEnableThinking(!enableThinking)}
                disabled={isStreaming}
                className={`
                  flex-shrink-0 w-14 h-14 flex items-center justify-center rounded-xl
                  transition-all duration-200 disabled:opacity-50 shadow-md hover:shadow-lg
                  ${enableThinking 
                    ? 'bg-gradient-to-br from-purple-500 to-purple-600 text-white scale-105' 
                    : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 border-2 border-gray-200 dark:border-gray-600'
                  }
                `}
                title={enableThinking ? "Thinking mode enabled - Extended reasoning active" : "Enable thinking mode - Show detailed reasoning process"}
              >
                <span className="text-2xl">🤔</span>
              </button>

              {/* Attach Button */}
              <button
                onClick={() => setShowFileUpload(!showFileUpload)}
                disabled={isStreaming}
                className={`
                  flex-shrink-0 w-14 h-14 flex items-center justify-center rounded-xl
                  transition-all duration-200 disabled:opacity-50 shadow-md hover:shadow-lg
                  ${showFileUpload 
                    ? 'bg-gradient-to-br from-blue-500 to-blue-600 text-white scale-105' 
                    : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 border-2 border-gray-200 dark:border-gray-600'
                  }
                `}
                title="Attach files (images, audio, documents)"
              >
                <span className="text-2xl">{showFileUpload ? '✕' : '📎'}</span>
              </button>

              {/* Text Input */}
              <input
                type="text"
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onKeyPress={(e) => e.key === 'Enter' && !e.shiftKey && handleSend()}
                placeholder={uploadedFiles.length > 0 ? "Add a message (optional)..." : "Type your message..."}
                disabled={isStreaming}
                className="flex-1 px-6 py-4 text-lg border-2 border-gray-200 dark:border-gray-600 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50 transition-all shadow-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 placeholder-gray-400 dark:placeholder-gray-500"
              />

              {/* Send Button */}
              <button
                onClick={handleSend}
                disabled={isStreaming || (!input.trim() && uploadedFiles.length === 0)}
                className="flex-shrink-0 px-8 py-4 bg-gradient-to-r from-blue-500 via-purple-500 to-pink-500 text-white rounded-xl hover:from-blue-600 hover:via-purple-600 hover:to-pink-600 disabled:opacity-50 disabled:cursor-not-allowed transition-all font-bold shadow-lg hover:shadow-xl transform hover:scale-105 active:scale-95 text-lg"
              >
                {isStreaming ? (
                  <span className="flex items-center gap-2">
                    <span className="animate-spin">⏳</span>
                    Processing
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    {uploadedFiles.length > 0 ? '📤' : '🚀'}
                    Send
                  </span>
                )}
              </button>
            </div>

            {uploadedFiles.length > 0 && (
              <p className="text-xs text-blue-600 dark:text-blue-400 font-medium mt-2 text-center">
                {uploadedFiles.length} file{uploadedFiles.length > 1 ? 's' : ''} attached · Ready to send
              </p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
