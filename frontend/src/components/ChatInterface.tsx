import React, { useEffect, useRef, useState } from 'react';
import { agentsApi } from '../services/api';
import { signalRService } from '../services/signalr';
import { useStore } from '../store/store';

type AgentName = 'InvestmentAdvisor' | 'PortfolioManager' | 'AccountServices' | 'ComplianceOfficer';

const AGENTS: Array<{ name: AgentName; icon: string; color: string; label: string }> = [
  { name: 'InvestmentAdvisor', icon: '📊', color: 'bg-blue-500', label: 'Investment Advisor' },
  { name: 'PortfolioManager', icon: '💼', color: 'bg-green-500', label: 'Portfolio Manager' },
  { name: 'AccountServices', icon: '🏦', color: 'bg-purple-500', label: 'Account Services' },
  { name: 'ComplianceOfficer', icon: '⚖️', color: 'bg-orange-500', label: 'Compliance Officer' },
];

export const ChatInterface: React.FC = () => {
  const [input, setInput] = useState('');
  const [useStreaming, setUseStreaming] = useState(true);
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const { agentChats, selectedAgent, setSelectedAgent, addMessage, updateLastMessage, setStreaming } = useStore();

  const currentChat = agentChats[selectedAgent];
  const messages = currentChat.messages;
  const isStreaming = currentChat.isStreaming;

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  useEffect(() => {
    if (useStreaming) {
      signalRService.connect()
        .then(() => setConnectionError(null))
        .catch((error) => {
          console.error('SignalR connection error:', error);
          setConnectionError(error.message || 'Failed to connect to chat server');
        });
    }
    return () => {
      if (useStreaming) {
        signalRService.disconnect().catch(console.error);
      }
    };
  }, [useStreaming]);

  const handleSend = async () => {
    if (!input.trim() || isStreaming) return;

    const userMessage = input.trim();
    setInput('');
    addMessage(selectedAgent, { text: userMessage, sender: 'user' });

    if (useStreaming) {
      await handleStreamingChat(userMessage);
    } else {
      await handleRegularChat(userMessage);
    }
  };

  const handleStreamingChat = async (message: string) => {
    setStreaming(selectedAgent, true);
    let fullResponse = '';
    let messageStarted = false;

    try {
      for await (const chunk of signalRService.chatStream(message, selectedAgent)) {
        if (!chunk.isComplete) {
          fullResponse += chunk.message;
          
          if (!messageStarted && fullResponse) {
            // Add initial agent message
            addMessage(selectedAgent, {
              text: fullResponse,
              sender: 'agent',
              agentName: selectedAgent,
            });
            messageStarted = true;
          } else if (messageStarted) {
            // Update the last message with accumulated response
            updateLastMessage(selectedAgent, fullResponse);
          }
        }
      }
    } catch (error) {
      console.error('Streaming error:', error);
      if (!messageStarted) {
        addMessage(selectedAgent, {
          text: 'Error: Failed to get response from agent',
          sender: 'agent',
          agentName: selectedAgent,
        });
      } else {
        updateLastMessage(selectedAgent, 'Error: Connection interrupted');
      }
    } finally {
      setStreaming(selectedAgent, false);
    }
  };

  const handleRegularChat = async (message: string) => {
    setStreaming(selectedAgent, true);

    try {
      const response = await agentsApi.chat({ message, agentName: selectedAgent });
      if (response.data.success && response.data.data) {
        addMessage(selectedAgent, {
          text: response.data.data.message,
          sender: 'agent',
          agentName: selectedAgent,
        });
      } else {
        throw new Error(response.data.error || 'Failed to get response');
      }
    } catch (error: any) {
      console.error('Chat error:', error);
      addMessage(selectedAgent, {
        text: `Error: ${error.message}`,
        sender: 'agent',
        agentName: selectedAgent,
      });
    } finally {
      setStreaming(selectedAgent, false);
    }
  };

  return (
    <div className="flex flex-col h-full">
      {/* Connection Error Banner */}
      {connectionError && (
        <div className="bg-red-50 border-b border-red-200 p-3">
          <div className="flex items-start gap-2">
            <span className="text-red-600 text-xl">⚠️</span>
            <div className="flex-1">
              <p className="text-red-800 font-medium text-sm">Connection Error</p>
              <p className="text-red-600 text-xs mt-1">{connectionError}</p>
              <p className="text-red-600 text-xs mt-1">
                Make sure the backend API is running with: <code className="bg-red-100 px-1 py-0.5 rounded">dotnet run</code>
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

      {/* Agent Tabs */}
      <div className="flex border-b bg-gray-50">
        {AGENTS.map((agent) => {
          const agentChat = agentChats[agent.name];
          const messageCount = agentChat.messages.length;
          const hasUnread = messageCount > 0;
          
          return (
            <button
              key={agent.name}
              onClick={() => setSelectedAgent(agent.name)}
              className={`flex items-center gap-2 px-4 py-3 border-b-2 transition-all relative ${
                selectedAgent === agent.name
                  ? 'border-blue-500 bg-white text-blue-600'
                  : 'border-transparent hover:bg-gray-100 text-gray-600'
              }`}
            >
              <span className="text-xl">{agent.icon}</span>
              <span className="font-medium text-sm">{agent.label}</span>
              {hasUnread && selectedAgent !== agent.name && (
                <span className="absolute top-2 right-2 w-2 h-2 bg-blue-500 rounded-full"></span>
              )}
              {messageCount > 0 && (
                <span className={`text-xs px-1.5 py-0.5 rounded-full ${
                  selectedAgent === agent.name ? 'bg-blue-100 text-blue-600' : 'bg-gray-200 text-gray-600'
                }`}>
                  {messageCount}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {messages.length === 0 && (
          <div className="text-center text-gray-500 mt-20">
            <p className="text-3xl mb-4">{AGENTS.find(a => a.name === selectedAgent)?.icon}</p>
            <p className="text-lg font-medium">
              Start a conversation with {AGENTS.find(a => a.name === selectedAgent)?.label}
            </p>
            <p className="text-sm mt-2">Ask about portfolios, funds, accounts, or investment advice</p>
          </div>
        )}

        {messages.map((msg) => (
          <div
            key={msg.id}
            className={`flex ${msg.sender === 'user' ? 'justify-end' : 'justify-start'}`}
          >
            <div
              className={`max-w-2xl px-4 py-3 rounded-lg ${
                msg.sender === 'user'
                  ? 'bg-blue-500 text-white'
                  : 'bg-gray-100 text-gray-900'
              }`}
            >
              {msg.sender === 'agent' && msg.agentName && (
                <div className="text-xs font-medium mb-1 opacity-75">{msg.agentName}</div>
              )}
              <div className="whitespace-pre-wrap">{msg.text}</div>
              <div className="text-xs mt-1 opacity-75">
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
        <div className="flex gap-2 mb-2">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={useStreaming}
              onChange={(e) => setUseStreaming(e.target.checked)}
              className="rounded"
            />
            Enable Streaming
          </label>
        </div>

        <div className="flex gap-2">
          <input
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyPress={(e) => e.key === 'Enter' && handleSend()}
            placeholder="Ask me anything..."
            disabled={isStreaming}
            className="flex-1 px-4 py-3 border rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
          />
          <button
            onClick={handleSend}
            disabled={isStreaming || !input.trim()}
            className="px-6 py-3 bg-blue-500 text-white rounded-lg hover:bg-blue-600 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            Send
          </button>
        </div>
      </div>
    </div>
  );
};
