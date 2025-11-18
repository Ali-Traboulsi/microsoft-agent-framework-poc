import React, { useEffect, useRef, useState } from 'react';
import { masterAgentService } from '../services/masterAgent';
import { MessageContent } from './MessageContent';

interface ChatMessage {
  id: string;
  type: 'user' | 'agent' | 'thinking' | 'delegation' | 'tool' | 'telemetry';
  content: string;
  timestamp: Date;
  subAgentName?: string;
  toolName?: string;
  metadata?: Record<string, any>;
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
    if (!input.trim() || isStreaming) return;

    const userMessage = input.trim();
    setInput('');
    
    // Add user message
    addMessage({ type: 'user', content: userMessage });

    // Reset telemetry
    const requestStart = Date.now();
    const delegationsUsed: string[] = [];
    const toolsUsed: string[] = [];

    setIsStreaming(true);
    let thinkingMessageId: string | null = null;
    let contentMessageId: string | null = null;
    let accumulatedThinking = '';
    let accumulatedContent = '';

    try {
      for await (const chunk of masterAgentService.chatStream(userMessage, conversationId.current)) {
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
            // Show tool execution
            if (chunk.toolName) {
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

  const getMessageStyle = (type: ChatMessage['type']) => {
    switch (type) {
      case 'user':
        return 'bg-blue-500 text-white ml-auto';
      case 'agent':
        return 'bg-gray-100 text-gray-900';
      case 'thinking':
        return 'bg-purple-50 text-purple-900 border border-purple-200';
      case 'delegation':
        return 'bg-blue-50 text-blue-900 border border-blue-200';
      case 'tool':
        return 'bg-green-50 text-green-900 border border-green-200';
      case 'telemetry':
        return 'bg-gray-50 text-gray-700 border border-gray-300 font-mono text-xs';
      default:
        return 'bg-gray-100 text-gray-900';
    }
  };

  const getMessageIcon = (type: ChatMessage['type']) => {
    switch (type) {
      case 'thinking':
        return '🤔';
      case 'delegation':
        return '🔄';
      case 'tool':
        return '🔧';
      case 'telemetry':
        return '📊';
      default:
        return null;
    }
  };

  return (
    <div className="flex flex-col h-full bg-white">
      {/* Header */}
      <div className="border-b bg-gradient-to-r from-blue-600 to-purple-600 text-white p-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <span className="text-3xl">🤖</span>
            <div>
              <h1 className="text-xl font-bold">Master Agent</h1>
              <p className="text-sm opacity-90">AI Orchestrator with Multi-Agent Coordination</p>
            </div>
          </div>
          <div className="flex items-center gap-4">
            <label className="flex items-center gap-2 text-sm cursor-pointer">
              <input
                type="checkbox"
                checked={showTelemetry}
                onChange={(e) => setShowTelemetry(e.target.checked)}
                className="rounded"
              />
              Show Telemetry
            </label>
            {telemetry.duration && (
              <div className="bg-white/20 px-3 py-1 rounded-lg text-sm">
                Last: {telemetry.duration}ms
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Connection Error Banner */}
      {connectionError && (
        <div className="bg-red-50 border-b border-red-200 p-3">
          <div className="flex items-start gap-2">
            <span className="text-red-600 text-xl">⚠️</span>
            <div className="flex-1">
              <p className="text-red-800 font-medium text-sm">Connection Error</p>
              <p className="text-red-600 text-xs mt-1">{connectionError}</p>
              <p className="text-red-600 text-xs mt-1">
                Make sure the backend is running: <code className="bg-red-100 px-1 py-0.5 rounded">cd src && dotnet run</code>
              </p>
            </div>
            <button
              onClick={() => setConnectionError(null)}
              className="text-red-600 hover:text-red-800"
            >
              ✕
            </button>
          </div>
        </div>
      )}

      {/* Telemetry Dashboard */}
      {showTelemetry && telemetry.duration && (
        <div className="border-b bg-gray-50 p-3">
          <div className="grid grid-cols-4 gap-4 text-sm">
            <div>
              <span className="text-gray-600">Duration:</span>
              <span className="ml-2 font-semibold">{telemetry.duration}ms</span>
            </div>
            <div>
              <span className="text-gray-600">Delegations:</span>
              <span className="ml-2 font-semibold">{telemetry.delegationCount || 0}</span>
            </div>
            <div>
              <span className="text-gray-600">Sub-Agents:</span>
              <span className="ml-2 font-semibold">{telemetry.subAgentsUsed?.join(', ') || 'None'}</span>
            </div>
            <div>
              <span className="text-gray-600">Tools:</span>
              <span className="ml-2 font-semibold">{telemetry.toolsUsed?.length || 0}</span>
            </div>
          </div>
        </div>
      )}

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-4 space-y-3">
        {messages.length === 0 && (
          <div className="text-center text-gray-500 mt-20">
            <p className="text-5xl mb-4">🤖</p>
            <p className="text-xl font-semibold mb-2">Master Agent Orchestrator</p>
            <p className="text-sm mb-4">
              I coordinate multiple specialized agents to handle your requests
            </p>
            <div className="grid grid-cols-2 gap-3 max-w-2xl mx-auto mt-6">
              {[
                { icon: '📊', name: 'Investment Advisor', desc: 'Fund research & recommendations' },
                { icon: '💼', name: 'Portfolio Manager', desc: 'Portfolio analysis & management' },
                { icon: '🏦', name: 'Account Services', desc: 'Account operations & balances' },
                { icon: '⚖️', name: 'Compliance Officer', desc: 'Risk assessment & compliance' }
              ].map((agent) => (
                <div key={agent.name} className="bg-gray-50 p-3 rounded-lg border">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="text-2xl">{agent.icon}</span>
                    <span className="font-medium text-sm">{agent.name}</span>
                  </div>
                  <p className="text-xs text-gray-600">{agent.desc}</p>
                </div>
              ))}
            </div>
          </div>
        )}

        {messages.map((msg) => (
          <div
            key={msg.id}
            className={`flex ${msg.type === 'user' ? 'justify-end' : 'justify-start'}`}
          >
            <div className={`max-w-3xl px-4 py-3 rounded-lg ${getMessageStyle(msg.type)}`}>
              {getMessageIcon(msg.type) && (
                <span className="inline-block mr-2">{getMessageIcon(msg.type)}</span>
              )}
              {msg.type === 'telemetry' ? (
                <pre className="whitespace-pre-wrap overflow-x-auto">{msg.content}</pre>
              ) : (
                <MessageContent 
                  content={msg.content} 
                  isAgent={msg.type !== 'user'} 
                />
              )}
              <div className={`text-xs mt-1 ${msg.type === 'user' ? 'opacity-75' : 'opacity-60'}`}>
                {msg.timestamp.toLocaleTimeString()}
              </div>
            </div>
          </div>
        ))}

        {isStreaming && (
          <div className="flex justify-start">
            <div className="bg-gray-100 px-4 py-3 rounded-lg">
              <div className="flex gap-1">
                <span className="animate-bounce">●</span>
                <span className="animate-bounce" style={{ animationDelay: '0.1s' }}>●</span>
                <span className="animate-bounce" style={{ animationDelay: '0.2s' }}>●</span>
              </div>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Input */}
      <div className="border-t p-4 bg-white">
        <div className="flex gap-2">
          <input
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyPress={(e) => e.key === 'Enter' && handleSend()}
            placeholder="Ask me anything about investments, portfolios, or accounts..."
            disabled={isStreaming}
            className="flex-1 px-4 py-3 border rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
          />
          <button
            onClick={handleSend}
            disabled={isStreaming || !input.trim()}
            className="px-6 py-3 bg-gradient-to-r from-blue-500 to-purple-500 text-white rounded-lg hover:from-blue-600 hover:to-purple-600 disabled:opacity-50 disabled:cursor-not-allowed transition-all font-medium"
          >
            {isStreaming ? 'Processing...' : 'Send'}
          </button>
        </div>
        <p className="text-xs text-gray-500 mt-2">
          💡 Try: "Show my portfolio and recommend tech funds" or "Create a balanced portfolio"
        </p>
      </div>
    </div>
  );
};
