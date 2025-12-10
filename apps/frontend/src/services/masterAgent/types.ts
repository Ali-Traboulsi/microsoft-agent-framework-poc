/**
 * Master Agent Service Types
 * All TypeScript interfaces and types for the master agent functionality
 */

import { WorkflowStep } from "../../components/Cards/WorkflowProgressCard";
import { UploadedFile } from "../../components/FileUpload";

// ============================================
// Stream Response Types
// ============================================

export interface MasterStreamResponse {
  type: string;
  content: string | null;
  subAgentName: string | null;
  toolName: string | null;
  isComplete: boolean;
  metadata: Record<string, unknown> | null;
  // Workflow progress fields
  stepId: string | null;
  stepName: string | null;
  stepNameAr: string | null;
  stepNumber: number | null;
  totalSteps: number | null;
  stepCompleted: boolean | null;
  stepDurationMs: number | null;
  stepDetails: string | null;
  // Projection result (included in Complete response for projection requests)
  projectionResult: ProjectionResult | null;
  // Thread ID (for thread-aware streaming)
  threadId?: string;
}

export interface ContentInput {
  Type: 'text' | 'image' | 'audio' | 'uri' | 'file';
  Text?: string;
  Data?: string;
  Uri?: string;
  MediaType?: string;
  FileName?: string;
}

// ============================================
// API Response Types
// ============================================

export interface MultiModalResponse {
  success: boolean;
  data: {
    message: string;
    timestamp: string;
    processingTimeMs: number;
    contentTypesProcessed: string[];
    conversationId: string;
  };
  error: string | null;
}

export interface ConversationStats {
  activeConversations: number;
  conversationIds: string[];
}

export interface ChatResponse {
  success: boolean;
  data: {
    success: boolean;
    response: string;
    subAgentsUsed: string[];
    durationMs: number;
    errorMessage: string | null;
    projectionResult: ProjectionResult | null;
    hasProjectionResult: boolean;
  };
  error: string | null;
}

// ============================================
// Projection Types
// ============================================

export interface ProjectionResult {
  projectionId: string;
  inputSummary: ProjectionInputSummary;
  scenarios: ProjectionScenarios;
  recommendedFunds: FundRecommendation[];
  riskWarnings: string[];
  riskWarningsAr: string[];
  callToAction: CallToAction;
  metadata: ProjectionMetadata;
}

export interface ProjectionInputSummary {
  amount: number;
  currency: string;
  horizon: string;
  horizonAr: string;
  riskProfile: string;
  riskProfileAr: string;
  investmentType: string;
  shariahCompliant: boolean;
}

export interface ProjectionScenarios {
  conservative: ProjectionScenario;
  expected: ProjectionScenario;
  optimistic: ProjectionScenario;
  strategyComparison?: StrategyComparison;
}

export interface ProjectionScenario {
  scenarioType: string;
  scenarioTypeAr: string;
  confidence: number;
  description: string;
  descriptionAr: string;
  assumedReturnRate: number;
  initialInvestment: number;
  projectedValue: number;
  totalReturn: number;
  annualizedReturn: number;
  monthlyProjections: MonthlyProjection[];
}

export interface MonthlyProjection {
  month: number;
  date: string;
  value: number;
  cumulativeReturn: number;
  monthlyContribution: number;
}

export interface StrategyComparison {
  lumpSum: StrategyDetails;
  monthlySip: StrategyDetails;
  recommendedStrategy: string;
  recommendationRationale: string;
  recommendationRationaleAr: string;
}

export interface StrategyDetails {
  strategyName: string;
  totalInvestment: number;
  projectedValue: number;
  totalReturn: number;
  benefit: string;
  benefitAr: string;
}

export interface FundRecommendation {
  fundCode: string;
  fundName: string;
  fundNameAr: string | null;
  fundType: string;
  allocationPercent: number;
  investmentAmount: number;
  expectedContribution: number;
  expectedReturn: number;
  reasons: string[];
  currentNav: number;
  isShariahCompliant: boolean;
}

export interface CallToAction {
  primaryAction: string;
  primaryActionAr: string;
  link: string;
  secondaryAction: string;
  secondaryActionAr: string;
  secondaryLink: string;
}

export interface ProjectionMetadata {
  createdAt: string;
  expiresAt: string;
  workflowVersion: string;
  executionTimeMs: number;
  dataSources: string[];
  customerId: string | null;
  usedHistoricalData: boolean;
  usedMarketData: boolean;
}


export interface ToolCallInfo {
  id: string;
  name: string;
  type: 'tool' | 'delegation';
  status: 'running' | 'completed' | 'error';
  startTime: Date;
  endTime?: Date;
  error?: string;
}

export interface ChatMessage {
  id: string;
  type: 'user' | 'agent' | 'thinking' | 'delegation' | 'tool' | 'telemetry' | 'multimodal' | 'transcription' | 'projection' | 'workflow-progress' | 'tool-calls';
  content: string;
  timestamp: Date;
  subAgentName?: string;
  toolName?: string;
  metadata?: Record<string, unknown>;
  files?: UploadedFile[]; // For displaying user's uploaded files
  projectionResult?: ProjectionResult; // For structured projection data
  workflowSteps?: WorkflowStep[]; // For workflow progress tracking
  toolCalls?: ToolCallInfo[]; // For consolidated tool call display
}

export interface TelemetryData {
  traceId?: string;
  duration?: number;
  delegationCount?: number;
  subAgentsUsed?: string[];
  toolsUsed?: string[];
}

export interface MultiModalResponse {
  success: boolean;
  data: {
    message: string;
    timestamp: string;
    processingTimeMs: number;
    contentTypesProcessed: string[];
    conversationId: string;
  };
  error: string | null;
}

export interface ConversationStats {
  activeConversations: number;
  conversationIds: string[];
}

// Projection Result Types for structured responses
export interface ProjectionResult {
  projectionId: string;
  inputSummary: {
    amount: number;
    currency: string;
    horizon: string;
    horizonAr: string;
    riskProfile: string;
    riskProfileAr: string;
    investmentType: string;
    shariahCompliant: boolean;
  };
  scenarios: {
    conservative: ProjectionScenario;
    expected: ProjectionScenario;
    optimistic: ProjectionScenario;
    strategyComparison?: StrategyComparison;
  };
  recommendedFunds: FundRecommendation[];
  riskWarnings: string[];
  riskWarningsAr: string[];
  callToAction: {
    primaryAction: string;
    primaryActionAr: string;
    link: string;
    secondaryAction: string;
    secondaryActionAr: string;
    secondaryLink: string;
  };
  metadata: {
    createdAt: string;
    expiresAt: string;
    workflowVersion: string;
    executionTimeMs: number;
    dataSources: string[];
    customerId: string | null;
    usedHistoricalData: boolean;
    usedMarketData: boolean;
  };
}

export interface ProjectionScenario {
  scenarioType: string;
  scenarioTypeAr: string;
  confidence: number;
  description: string;
  descriptionAr: string;
  assumedReturnRate: number;
  initialInvestment: number;
  projectedValue: number;
  totalReturn: number;
  annualizedReturn: number;
  monthlyProjections: MonthlyProjection[];
}

export interface MonthlyProjection {
  month: number;
  date: string;
  value: number;
  cumulativeReturn: number;
  monthlyContribution: number;
}

export interface StrategyComparison {
  lumpSum: {
    strategyName: string;
    totalInvestment: number;
    projectedValue: number;
    totalReturn: number;
    benefit: string;
    benefitAr: string;
  };
  monthlySip: {
    strategyName: string;
    totalInvestment: number;
    projectedValue: number;
    totalReturn: number;
    benefit: string;
    benefitAr: string;
  };
  recommendedStrategy: string;
  recommendationRationale: string;
  recommendationRationaleAr: string;
}

export interface FundRecommendation {
  fundCode: string;
  fundName: string;
  fundNameAr: string | null;
  fundType: string;
  allocationPercent: number;
  investmentAmount: number;
  expectedContribution: number;
  expectedReturn: number;
  reasons: string[];
  currentNav: number;
  isShariahCompliant: boolean;
}

export interface ChatResponse {
  success: boolean;
  data: {
    success: boolean;
    response: string;
    subAgentsUsed: string[];
    durationMs: number;
    errorMessage: string | null;
    projectionResult: ProjectionResult | null;
    hasProjectionResult: boolean;
  };
  error: string | null;
}


