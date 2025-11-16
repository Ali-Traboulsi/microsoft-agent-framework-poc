import React from 'react';
import { AccountsView } from './components/AccountsView';
import { ChatInterface } from './components/ChatInterface';
import { FundsView } from './components/FundsView';
import { PortfolioView } from './components/PortfolioView';
import './index.css';
import { useStore } from './store/store';

const App: React.FC = () => {
  const { activeTab, setActiveTab } = useStore();

  const tabs = [
    { id: 'chat', label: 'Agent Chat', icon: '💬' },
    { id: 'portfolios', label: 'Portfolios', icon: '💼' },
    { id: 'funds', label: 'Mutual Funds', icon: '📊' },
    { id: 'accounts', label: 'Accounts', icon: '🏦' },
  ] as const;

  return (
    <div className="flex flex-col h-screen bg-gray-100">
      {/* Header */}
      <header className="bg-white shadow-sm">
        <div className="max-w-7xl mx-auto px-4 py-4">
          <h1 className="text-2xl font-bold text-gray-900">
            🏦 Banking Investment Agent System
          </h1>
          <p className="text-sm text-gray-600 mt-1">
            AI-powered investment advisory and portfolio management
          </p>
        </div>
      </header>

      {/* Navigation */}
      <nav className="bg-white border-b">
        <div className="max-w-7xl mx-auto px-4">
          <div className="flex gap-1">
            {tabs.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`px-6 py-3 font-medium transition-colors border-b-2 ${
                  activeTab === tab.id
                    ? 'border-blue-500 text-blue-600'
                    : 'border-transparent text-gray-600 hover:text-gray-900'
                }`}
              >
                <span className="mr-2">{tab.icon}</span>
                {tab.label}
              </button>
            ))}
          </div>
        </div>
      </nav>

      {/* Main Content */}
      <main className="flex-1 overflow-hidden max-w-7xl mx-auto w-full">
        {activeTab === 'chat' && <ChatInterface />}
        {activeTab === 'portfolios' && <PortfolioView />}
        {activeTab === 'funds' && <FundsView />}
        {activeTab === 'accounts' && <AccountsView />}
      </main>

      {/* Footer */}
      <footer className="bg-white border-t py-4">
        <div className="max-w-7xl mx-auto px-4 text-center text-sm text-gray-600">
          Microsoft Agent Framework • Built with React & TypeScript • SignalR Streaming
        </div>
      </footer>
    </div>
  );
};

export default App;
