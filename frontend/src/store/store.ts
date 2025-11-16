import { create } from 'zustand';
import { Account, MutualFund, Portfolio } from '../services/api';

interface Message {
  id: string;
  text: string;
  sender: 'user' | 'agent';
  agentName?: string;
  timestamp: Date;
}

interface AgentChat {
  messages: Message[];
  isStreaming: boolean;
}

type AgentName = 'InvestmentAdvisor' | 'PortfolioManager' | 'AccountServices' | 'ComplianceOfficer';

interface AppState {
  // UI State
  activeTab: 'chat' | 'portfolios' | 'funds' | 'accounts';
  setActiveTab: (tab: 'chat' | 'portfolios' | 'funds' | 'accounts') => void;

  // Chat State - Per Agent
  agentChats: Record<AgentName, AgentChat>;
  selectedAgent: AgentName;
  setSelectedAgent: (agent: AgentName) => void;
  addMessage: (agentName: AgentName, message: Omit<Message, 'id' | 'timestamp'>) => void;
  updateLastMessage: (agentName: AgentName, text: string) => void;
  setStreaming: (agentName: AgentName, streaming: boolean) => void;
  clearMessages: (agentName: AgentName) => void;
  clearAllMessages: () => void;

  // Data State
  accounts: Account[];
  portfolios: Portfolio[];
  funds: MutualFund[];
  setAccounts: (accounts: Account[]) => void;
  setPortfolios: (portfolios: Portfolio[]) => void;
  setFunds: (funds: MutualFund[]) => void;

  // Selected Items
  selectedAccount: Account | null;
  selectedPortfolio: Portfolio | null;
  selectedFund: MutualFund | null;
  setSelectedAccount: (account: Account | null) => void;
  setSelectedPortfolio: (portfolio: Portfolio | null) => void;
  setSelectedFund: (fund: MutualFund | null) => void;
}

const initialAgentChat: AgentChat = {
  messages: [],
  isStreaming: false,
};

export const useStore = create<AppState>((set) => ({
  // UI State
  activeTab: 'chat',
  setActiveTab: (tab) => set({ activeTab: tab }),

  // Chat State - Per Agent
  agentChats: {
    InvestmentAdvisor: { ...initialAgentChat },
    PortfolioManager: { ...initialAgentChat },
    AccountServices: { ...initialAgentChat },
    ComplianceOfficer: { ...initialAgentChat },
  },
  selectedAgent: 'InvestmentAdvisor',
  setSelectedAgent: (agent) => set({ selectedAgent: agent }),
  addMessage: (agentName, message) =>
    set((state) => ({
      agentChats: {
        ...state.agentChats,
        [agentName]: {
          ...state.agentChats[agentName],
          messages: [
            ...state.agentChats[agentName].messages,
            {
              ...message,
              id: Math.random().toString(36).substr(2, 9),
              timestamp: new Date(),
            },
          ],
        },
      },
    })),
  updateLastMessage: (agentName, text) =>
    set((state) => {
      const agentChat = state.agentChats[agentName];
      if (agentChat.messages.length === 0) return state;
      
      const updatedMessages = [...agentChat.messages];
      const lastMessage = updatedMessages[updatedMessages.length - 1];
      
      if (lastMessage.sender === 'agent') {
        updatedMessages[updatedMessages.length - 1] = {
          ...lastMessage,
          text,
        };
      }
      
      return {
        agentChats: {
          ...state.agentChats,
          [agentName]: {
            ...agentChat,
            messages: updatedMessages,
          },
        },
      };
    }),
  setStreaming: (agentName, streaming) =>
    set((state) => ({
      agentChats: {
        ...state.agentChats,
        [agentName]: {
          ...state.agentChats[agentName],
          isStreaming: streaming,
        },
      },
    })),
  clearMessages: (agentName) =>
    set((state) => ({
      agentChats: {
        ...state.agentChats,
        [agentName]: {
          ...initialAgentChat,
        },
      },
    })),
  clearAllMessages: () =>
    set({
      agentChats: {
        InvestmentAdvisor: { ...initialAgentChat },
        PortfolioManager: { ...initialAgentChat },
        AccountServices: { ...initialAgentChat },
        ComplianceOfficer: { ...initialAgentChat },
      },
    }),

  // Data State
  accounts: [],
  portfolios: [],
  funds: [],
  setAccounts: (accounts) => set({ accounts }),
  setPortfolios: (portfolios) => set({ portfolios }),
  setFunds: (funds) => set({ funds }),

  // Selected Items
  selectedAccount: null,
  selectedPortfolio: null,
  selectedFund: null,
  setSelectedAccount: (account) => set({ selectedAccount: account }),
  setSelectedPortfolio: (portfolio) => set({ selectedPortfolio: portfolio }),
  setSelectedFund: (fund) => set({ selectedFund: fund }),
}));
