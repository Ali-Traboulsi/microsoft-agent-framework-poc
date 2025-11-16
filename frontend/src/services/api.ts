import axios from 'axios';

const api = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error: string | null;
}

export interface Account {
  accountId: string;
  customerId: string;
  customerName: string;
  balance: number;
  currency: string;
  createdDate: string;
  status: number; // Enum: 0=Active, 1=Suspended, 2=Closed
}

export interface Portfolio {
  portfolioId: string;
  accountId: string;
  portfolioName: string;
  strategy: string;
  createdDate: string;
  totalValue: number;
  holdings: PortfolioHolding[];
}

export interface PortfolioHolding {
  fundId: string;
  fundSymbol: string;
  fundName: string;
  shares: number;
  averageCost: number;
  currentValue: number;
  gainLoss: number;
  gainLossPercentage: number;
}

export interface MutualFund {
  fundId: string;
  fundSymbol: string;
  fundName: string;
  category: string;
  riskLevel: number; // Enum: 0=Low, 1=Medium, 2=High, 3=VeryHigh
  nav: number;
  oneYearReturn: number;
  threeYearReturn: number;
  fiveYearReturn: number;
  expenseRatio: number;
  minInvestment: number;
}

export interface ChatRequest {
  message: string;
  agentName: string;
}

export interface ChatResponse {
  message: string;
  agentName: string;
  timestamp: string;
}

// Accounts API
export const accountsApi = {
  getAll: () => api.get<ApiResponse<Account[]>>('/accounts'),
  getById: (id: string) => api.get<ApiResponse<Account>>(`/accounts/${id}`),
  getBalance: (id: string) => api.get<ApiResponse<string>>(`/accounts/${id}/balance`),
  deposit: (id: string, amount: number) => 
    api.post<ApiResponse<string>>(`/accounts/${id}/deposit`, amount),
  getTransactions: (id: string, limit = 10) => 
    api.get<ApiResponse<any[]>>(`/accounts/${id}/transactions?limit=${limit}`),
};

// Portfolios API
export const portfoliosApi = {
  getAll: (accountId?: string) => 
    api.get<ApiResponse<Portfolio[]>>(`/portfolios${accountId ? `?accountId=${accountId}` : ''}`),
  getById: (id: string) => api.get<ApiResponse<Portfolio>>(`/portfolios/${id}`),
  create: (request: { accountId: string; portfolioName: string; strategy: string }) => 
    api.post<ApiResponse<string>>('/portfolios', request),
  getDetails: (id: string) => api.get<ApiResponse<string>>(`/portfolios/${id}/details`),
  getAllocation: (id: string) => api.get<ApiResponse<string>>(`/portfolios/${id}/allocation`),
  invest: (id: string, request: { accountId: string; portfolioId: string; fundSymbol: string; amount: number }) => 
    api.post<ApiResponse<string>>(`/portfolios/${id}/invest`, request),
};

// Funds API
export const fundsApi = {
  getAll: () => api.get<ApiResponse<MutualFund[]>>('/funds'),
  getById: (id: string) => api.get<ApiResponse<MutualFund>>(`/funds/${id}`),
  search: (request: { category?: string; riskLevel?: string; minReturn?: number }) => 
    api.post<ApiResponse<string>>('/funds/search', request),
  compare: (symbols: string[]) => api.post<ApiResponse<string>>('/funds/compare', symbols),
  getDetails: (id: string) => api.get<ApiResponse<string>>(`/funds/${id}/details`),
};

// Agents API
export const agentsApi = {
  getAvailable: () => api.get<ApiResponse<string[]>>('/agents'),
  chat: (request: ChatRequest) => api.post<ApiResponse<ChatResponse>>('/agents/chat', request),
};

export default api;
