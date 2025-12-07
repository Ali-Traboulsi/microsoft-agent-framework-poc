export interface MonthlyProjection {
  month: number;
  date: string;
  value: number;
  cumulativeReturn: number;
  monthlyContribution: number;
}

export interface Scenario {
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

export interface Scenarios {
  conservative: Scenario;
  expected: Scenario;
  optimistic: Scenario;
  strategyComparison?: StrategyComparison;
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

export interface InputSummary {
  amount: number;
  currency: string;
  horizon: string;
  horizonAr: string;
  riskProfile: string;
  riskProfileAr: string;
  investmentType: string;
  shariahCompliant: boolean;
}

export interface CallToAction {
  primaryAction: string;
  primaryActionAr: string;
  link: string;
  secondaryAction: string;
  secondaryActionAr: string;
  secondaryLink: string;
}

export interface Metadata {
  createdAt: string;
  expiresAt: string;
  workflowVersion: string;
  executionTimeMs: number;
  dataSources: string[];
  customerId: string | null;
  usedHistoricalData: boolean;
  usedMarketData: boolean;
}

export interface ProjectionResult {
  projectionId: string;
  inputSummary: InputSummary;
  scenarios: Scenarios;
  recommendedFunds: FundRecommendation[];
  riskWarnings: string[];
  riskWarningsAr: string[];
  callToAction: CallToAction;
  metadata: Metadata;
}

export interface ProjectionResultCardProps {
  result: ProjectionResult;
}



