import React from 'react';
import { MasterAgentChat } from './components/MasterAgentChat/index';
import './index.css';

const App: React.FC = () => {
  return (
    <div className="flex flex-col h-screen bg-gray-100">
      {/* Main Content */}
      <main className="flex-1 overflow-hidden w-full">
        <MasterAgentChat />
      </main>
    </div>
  );
};

export default App;
