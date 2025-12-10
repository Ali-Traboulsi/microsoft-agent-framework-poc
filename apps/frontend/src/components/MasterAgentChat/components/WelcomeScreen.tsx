import React from 'react';

export const WelcomeScreen: React.FC = () => {
  return (
    <div className="flex-1 flex items-center justify-center p-8">
      <div className="text-center max-w-lg">
        <div className="w-20 h-20 bg-gradient-to-br from-blue-500 to-purple-600 rounded-3xl flex items-center justify-center mx-auto mb-6 shadow-lg shadow-blue-500/25">
          <span className="text-5xl">🏦</span>
        </div>
        <h2 className="text-2xl font-bold text-slate-800 dark:text-white mb-3">
          Welcome to SNB Capital Investment Platform
        </h2>
        <p className="text-slate-600 dark:text-slate-400 mb-6">
          I'm your Master Agent, ready to help with portfolio management, investment advice,
          account services, and compliance inquiries. How can I assist you today?
        </p>
        <div className="grid grid-cols-2 gap-3 text-left">
          <SuggestionCard
            emoji="💼"
            text="Check my portfolio performance"
          />
          <SuggestionCard
            emoji="📊"
            text="Get investment recommendations"
          />
          <SuggestionCard
            emoji="💰"
            text="View account balances"
          />
          <SuggestionCard
            emoji="📋"
            text="Submit compliance inquiry"
          />
        </div>
      </div>
    </div>
  );
};

interface SuggestionCardProps {
  emoji: string;
  text: string;
}

const SuggestionCard: React.FC<SuggestionCardProps> = ({ emoji, text }) => {
  return (
    <div className="p-3 bg-slate-100 dark:bg-slate-700/50 rounded-xl text-sm text-slate-600 dark:text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-700 cursor-pointer transition-colors">
      <span className="mr-2">{emoji}</span>
      {text}
    </div>
  );
};
