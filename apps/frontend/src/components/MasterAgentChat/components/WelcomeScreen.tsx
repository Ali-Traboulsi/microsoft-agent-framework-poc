import React, { useEffect, useState } from 'react';

interface WelcomeScreenProps {
  onSuggestionClick?: (text: string) => void;
}

// Typewriter hook
const useTypewriter = (text: string, speed: number = 50, delay: number = 0) => {
  const [displayText, setDisplayText] = useState('');
  const [isComplete, setIsComplete] = useState(false);

  useEffect(() => {
    setDisplayText('');
    setIsComplete(false);
    
    const timeout = setTimeout(() => {
      let i = 0;
      const interval = setInterval(() => {
        if (i < text.length) {
          setDisplayText(text.slice(0, i + 1));
          i++;
        } else {
          setIsComplete(true);
          clearInterval(interval);
        }
      }, speed);
      return () => clearInterval(interval);
    }, delay);
    
    return () => clearTimeout(timeout);
  }, [text, speed, delay]);

  return { displayText, isComplete };
};

export const WelcomeScreen: React.FC<WelcomeScreenProps> = ({ onSuggestionClick }) => {
  const [activePrompt, setActivePrompt] = useState(0);
  const { displayText, isComplete } = useTypewriter("How can I help you today?", 60, 500);

  const prompts = [
    { text: "Analyze my portfolio performance", icon: "💼" },
    { text: "Find high-growth investment funds", icon: "📈" },
    { text: "Check my account balances", icon: "🏦" },
    { text: "Search latest market trends", icon: "🌐" },
  ];

  // Rotate prompts
  useEffect(() => {
    const interval = setInterval(() => {
      setActivePrompt((prev) => (prev + 1) % prompts.length);
    }, 3000);
    return () => clearInterval(interval);
  }, [prompts.length]);

  const agents = [
    { icon: '💼', name: 'Portfolio', color: 'bg-blue-500' },
    { icon: '📊', name: 'Advisor', color: 'bg-purple-500' },
    { icon: '🏦', name: 'Accounts', color: 'bg-emerald-500' },
    { icon: '⚖️', name: 'Compliance', color: 'bg-amber-500' },
  ];

  return (
    <div className="flex-1 flex items-center justify-center h-full">
      <div className="w-full max-w-3xl mx-auto px-6">
        
        {/* Hero Section - Compact */}
        <div className="text-center mb-8">
          {/* Floating Agents Ring */}
          <div className="relative w-24 h-24 mx-auto mb-6">
            {/* Central Logo */}
            <div className="absolute inset-0 flex items-center justify-center">
              <div className="w-16 h-16 bg-gradient-to-br from-violet-500 via-purple-500 to-fuchsia-500 rounded-2xl flex items-center justify-center shadow-lg shadow-purple-500/30">
                <span className="text-3xl">🤖</span>
              </div>
            </div>
            
            {/* Orbiting Agents */}
            {agents.map((agent, idx) => (
              <div
                key={idx}
                className="absolute w-8 h-8 rounded-full flex items-center justify-center shadow-md animate-spin"
                style={{
                  animation: `orbit 8s linear infinite`,
                  animationDelay: `${idx * -2}s`,
                  top: '50%',
                  left: '50%',
                  transformOrigin: '0 0',
                }}
              >
                <div 
                  className={`w-8 h-8 ${agent.color} rounded-full flex items-center justify-center text-sm shadow-lg`}
                  style={{ animation: `counter-orbit 8s linear infinite`, animationDelay: `${idx * -2}s` }}
                >
                  {agent.icon}
                </div>
              </div>
            ))}
          </div>

          {/* Title */}
          <h1 className="text-3xl font-bold text-slate-800 dark:text-white mb-2">
            SNB Capital <span className="bg-gradient-to-r from-violet-500 to-fuchsia-500 bg-clip-text text-transparent">AI Assistant</span>
          </h1>
          
          {/* Typewriter Text */}
          <p className="text-lg text-slate-500 dark:text-slate-400 h-7">
            {displayText}
            {!isComplete && <span className="animate-pulse ml-0.5 text-violet-500">|</span>}
          </p>
        </div>

        {/* Animated Prompt Showcase */}
        <div className="mb-8">
          <div 
            onClick={() => onSuggestionClick?.(prompts[activePrompt].text)}
            className="group relative mx-auto max-w-lg cursor-pointer"
          >
            <div className="absolute -inset-1 bg-gradient-to-r from-violet-500 via-purple-500 to-fuchsia-500 rounded-2xl blur opacity-25 group-hover:opacity-50 transition duration-300" />
            <div className="relative bg-white dark:bg-slate-800 rounded-xl p-4 border border-slate-200 dark:border-slate-700 group-hover:border-transparent transition-all">
              <div className="flex items-center gap-3">
                <div className="flex-shrink-0 w-10 h-10 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-xl flex items-center justify-center">
                  <span className="text-xl transition-transform group-hover:scale-125 duration-300">
                    {prompts[activePrompt].icon}
                  </span>
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-xs text-slate-400 mb-0.5">Try asking...</p>
                  <p className="text-slate-700 dark:text-slate-200 font-medium truncate">
                    "{prompts[activePrompt].text}"
                  </p>
                </div>
                <div className="flex-shrink-0 w-8 h-8 bg-violet-100 dark:bg-violet-900/30 rounded-full flex items-center justify-center group-hover:bg-violet-500 transition-colors">
                  <span className="text-violet-500 group-hover:text-white transition-colors">→</span>
                </div>
              </div>
              
              {/* Progress dots */}
              <div className="flex justify-center gap-1.5 mt-3">
                {prompts.map((_, idx) => (
                  <button
                    key={idx}
                    onClick={(e) => { e.stopPropagation(); setActivePrompt(idx); }}
                    className={`w-1.5 h-1.5 rounded-full transition-all duration-300 ${
                      idx === activePrompt 
                        ? 'w-4 bg-violet-500' 
                        : 'bg-slate-300 dark:bg-slate-600 hover:bg-violet-300'
                    }`}
                  />
                ))}
              </div>
            </div>
          </div>
        </div>

        {/* Quick Actions - Horizontal Pills */}
        <div className="flex flex-wrap justify-center gap-2 mb-6">
          {[
            { emoji: '🚀', text: 'Run full analysis' },
            { emoji: '📊', text: 'Get recommendations' },
            { emoji: '🔍', text: 'Search markets' },
            { emoji: '📋', text: 'Compliance check' },
          ].map((action, idx) => (
            <button
              key={idx}
              onClick={() => onSuggestionClick?.(action.text)}
              className="group flex items-center gap-2 px-4 py-2 bg-slate-100 dark:bg-slate-800 hover:bg-gradient-to-r hover:from-violet-500 hover:to-fuchsia-500 rounded-full transition-all duration-300 hover:shadow-lg hover:shadow-violet-500/25"
            >
              <span className="text-base group-hover:scale-110 transition-transform">{action.emoji}</span>
              <span className="text-sm font-medium text-slate-600 dark:text-slate-300 group-hover:text-white transition-colors">
                {action.text}
              </span>
            </button>
          ))}
        </div>

        {/* Status Footer */}
        <div className="flex items-center justify-center gap-4 text-xs text-slate-400">
          <div className="flex items-center gap-1.5">
            <span className="relative flex h-2 w-2">
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-green-400 opacity-75"></span>
              <span className="relative inline-flex rounded-full h-2 w-2 bg-green-500"></span>
            </span>
            <span>Online</span>
          </div>
          <span>•</span>
          <span>4 AI Agents Ready</span>
          <span>•</span>
          <span>🔒 Encrypted</span>
        </div>
      </div>

      {/* CSS for orbit animation */}
      <style>{`
        @keyframes orbit {
          from { transform: rotate(0deg) translateX(44px) rotate(0deg); }
          to { transform: rotate(360deg) translateX(44px) rotate(-360deg); }
        }
        @keyframes counter-orbit {
          from { transform: rotate(0deg); }
          to { transform: rotate(-360deg); }
        }
      `}</style>
    </div>
  );
};
