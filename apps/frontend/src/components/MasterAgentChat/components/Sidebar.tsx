import React from 'react';
import type { ChatThread } from '../../../services/threads';
import ThreadList from '../../ThreadList';

interface SidebarProps {
  collapsed: boolean;
  onToggleCollapse: () => void;
  connectionError: string | null;
  showThreadList: boolean;
  onToggleView: (show: boolean) => void;
  currentThreadId: string | null;
  onSelectThread: (thread: ChatThread | null) => Promise<void>;
  onNewChat: () => void;
  threadRefreshTrigger: number;
  conversationId: string;
}

const AVAILABLE_AGENTS = [
  { icon: '📊', name: 'Investment Advisor', desc: 'Fund research' },
  { icon: '💼', name: 'Portfolio Manager', desc: 'Portfolio mgmt' },
  { icon: '🏦', name: 'Account Services', desc: 'Account ops' },
  { icon: '⚖️', name: 'Compliance Officer', desc: 'Risk & compliance' },
];

export const Sidebar: React.FC<SidebarProps> = ({
  collapsed,
  onToggleCollapse,
  connectionError,
  showThreadList,
  onToggleView,
  currentThreadId,
  onSelectThread,
  onNewChat,
  threadRefreshTrigger,
  conversationId,
}) => {
  return (
    <div
      className={`${
        collapsed ? 'w-16' : 'w-80'
      } bg-gradient-to-b from-slate-900 via-slate-800 to-slate-900 text-white flex flex-col transition-all duration-300 border-r border-slate-700 shadow-2xl`}
    >
      {/* Header */}
      <div className="p-6 border-b border-slate-700">
        <div className="flex items-center justify-between mb-4">
          {!collapsed && (
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
            onClick={onToggleCollapse}
            className="p-2 hover:bg-slate-700 rounded-lg transition-colors"
            title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          >
            <span className="text-xl">{collapsed ? '☰' : '‹'}</span>
          </button>
        </div>
      </div>

      {/* Connection Status */}
      {!collapsed && (
        <div className="px-6 py-4 border-b border-slate-700">
          <div className="flex items-center gap-2 text-sm">
            <div
              className={`w-2 h-2 rounded-full ${
                connectionError ? 'bg-red-500 animate-pulse' : 'bg-green-500'
              }`}
            />
            <span className="text-slate-300">{connectionError ? 'Disconnected' : 'Connected'}</span>
          </div>
        </div>
      )}

      {/* Thread List / Agents Toggle */}
      {!collapsed && (
        <div className="flex-1 overflow-hidden flex flex-col">
          <div className="flex border-b border-slate-700">
            <button
              onClick={() => onToggleView(true)}
              className={`flex-1 px-4 py-2 text-xs font-semibold uppercase tracking-wider transition-colors ${
                showThreadList
                  ? 'bg-slate-800 text-white border-b-2 border-blue-500'
                  : 'text-slate-400 hover:text-white hover:bg-slate-800/50'
              }`}
            >
              💬 Chats
            </button>
            <button
              onClick={() => onToggleView(false)}
              className={`flex-1 px-4 py-2 text-xs font-semibold uppercase tracking-wider transition-colors ${
                !showThreadList
                  ? 'bg-slate-800 text-white border-b-2 border-blue-500'
                  : 'text-slate-400 hover:text-white hover:bg-slate-800/50'
              }`}
            >
              🤖 Agents
            </button>
          </div>

          {showThreadList ? (
            <ThreadList
              selectedThreadId={currentThreadId}
              onSelectThread={onSelectThread}
              onNewChat={onNewChat}
              refreshTrigger={threadRefreshTrigger}
            />
          ) : (
            <div className="flex-1 overflow-y-auto px-6 py-4">
              <h3 className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-3">
                Available Agents
              </h3>
              <div className="space-y-2">
                {AVAILABLE_AGENTS.map((agent) => (
                  <div
                    key={agent.name}
                    className="bg-slate-800/40 p-3 rounded-lg hover:bg-slate-800/60 transition-colors"
                  >
                    <div className="flex items-center gap-2 mb-1">
                      <span className="text-lg">{agent.icon}</span>
                      <span className="font-semibold text-sm">{agent.name}</span>
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
        </div>
      )}

      {/* Footer */}
      {!collapsed && (
        <div className="p-4 border-t border-slate-700 space-y-2">
          <div className="text-xs">
            <div className="text-slate-400 mb-1">Conversation ID</div>
            <div className="font-mono text-slate-300 bg-slate-800/50 px-2 py-1 rounded text-[10px] break-all">
              {conversationId}
            </div>
          </div>
          <div className="flex items-center gap-2 text-xs text-slate-500">
            <div className="w-2 h-2 rounded-full bg-green-500"></div>
            <span>History tracked on server</span>
          </div>
        </div>
      )}
    </div>
  );
};
