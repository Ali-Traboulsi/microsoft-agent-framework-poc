import React, { useState } from 'react';
import {
    Area,
    AreaChart,
    Bar,
    BarChart,
    CartesianGrid,
    Cell,
    Legend,
    Pie,
    PieChart,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis
} from 'recharts';

// Types based on the API response structure
interface MonthlyProjection {
  month: number;
  date: string;
  value: number;
  cumulativeReturn: number;
  monthlyContribution: number;
}

interface Scenario {
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

interface StrategyComparison {
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

interface Scenarios {
  conservative: Scenario;
  expected: Scenario;
  optimistic: Scenario;
  strategyComparison?: StrategyComparison;
}

interface FundRecommendation {
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

interface InputSummary {
  amount: number;
  currency: string;
  horizon: string;
  horizonAr: string;
  riskProfile: string;
  riskProfileAr: string;
  investmentType: string;
  shariahCompliant: boolean;
}

interface CallToAction {
  primaryAction: string;
  primaryActionAr: string;
  link: string;
  secondaryAction: string;
  secondaryActionAr: string;
  secondaryLink: string;
}

interface Metadata {
  createdAt: string;
  expiresAt: string;
  workflowVersion: string;
  executionTimeMs: number;
  dataSources: string[];
  customerId: string | null;
  usedHistoricalData: boolean;
  usedMarketData: boolean;
}

interface ProjectionResult {
  projectionId: string;
  inputSummary: InputSummary;
  scenarios: Scenarios;
  recommendedFunds: FundRecommendation[];
  riskWarnings: string[];
  riskWarningsAr: string[];
  callToAction: CallToAction;
  metadata: Metadata;
}

interface ProjectionResultCardProps {
  result: ProjectionResult;
}

// Chart colors
const COLORS = {
  conservative: '#22c55e', // green
  expected: '#3b82f6', // blue
  optimistic: '#8b5cf6', // purple
  gradient: ['#06b6d4', '#3b82f6', '#8b5cf6', '#ec4899', '#f97316'],
  funds: ['#3b82f6', '#22c55e', '#f59e0b', '#ec4899', '#8b5cf6', '#06b6d4', '#f97316'],
};

// Format currency
const formatCurrency = (value: number, currency: string = 'SAR') => {
  return new Intl.NumberFormat('en-SA', {
    style: 'currency',
    currency: currency,
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value);
};

// Format percentage
const formatPercent = (value: number) => `${value.toFixed(1)}%`;

// Custom tooltip for charts
const CustomTooltip = ({ active, payload, label }: any) => {
  if (active && payload && payload.length) {
    return (
      <div className="bg-white/95 backdrop-blur-sm border border-gray-200 rounded-lg shadow-xl p-3">
        <p className="font-semibold text-gray-800 mb-2">Month {label}</p>
        {payload.map((entry: any, index: number) => (
          <p key={index} className="text-sm" style={{ color: entry.color }}>
            {entry.name}: {formatCurrency(entry.value)}
          </p>
        ))}
      </div>
    );
  }
  return null;
};

// Scenario Card Component
const ScenarioCard: React.FC<{
  scenario: Scenario;
  colorClass: string;
  icon: string;
  isHighlighted?: boolean;
}> = ({ scenario, colorClass, icon, isHighlighted }) => (
  <div
    className={`relative overflow-hidden rounded-xl border-2 p-5 transition-all duration-300 hover:shadow-lg ${
      isHighlighted
        ? 'border-blue-400 bg-gradient-to-br from-blue-50 to-indigo-50 shadow-md'
        : 'border-gray-200 bg-white hover:border-gray-300'
    }`}
  >
    {isHighlighted && (
      <div className="absolute top-0 right-0 bg-blue-500 text-white text-xs font-bold px-3 py-1 rounded-bl-lg">
        MOST LIKELY
      </div>
    )}
    <div className="flex items-start gap-3 mb-4">
      <span className="text-3xl">{icon}</span>
      <div>
        <h4 className={`font-bold text-lg ${colorClass}`}>{scenario.scenarioType}</h4>
        <p className="text-xs text-gray-500">{scenario.confidence}% Confidence</p>
      </div>
    </div>

    <div className="space-y-3">
      <div className="flex justify-between items-center py-2 border-b border-gray-100">
        <span className="text-gray-600 text-sm">Projected Value</span>
        <span className="font-bold text-xl text-gray-900">
          {formatCurrency(scenario.projectedValue)}
        </span>
      </div>
      <div className="flex justify-between items-center py-2 border-b border-gray-100">
        <span className="text-gray-600 text-sm">Total Return</span>
        <span className={`font-semibold text-lg ${colorClass}`}>
          +{formatCurrency(scenario.totalReturn)}
        </span>
      </div>
      <div className="flex justify-between items-center py-2">
        <span className="text-gray-600 text-sm">Annualized Return</span>
        <span className={`font-bold text-lg ${colorClass}`}>
          {formatPercent(scenario.annualizedReturn)}
        </span>
      </div>
    </div>

    <p className="mt-4 text-xs text-gray-500 italic leading-relaxed">{scenario.description}</p>
  </div>
);

// Fund Allocation Pie Chart
const FundAllocationChart: React.FC<{ funds: FundRecommendation[] }> = ({ funds }) => {
  const data = funds.map((fund) => ({
    name: fund.fundCode,
    fullName: fund.fundName,
    value: fund.allocationPercent,
    amount: fund.investmentAmount,
    expectedReturn: fund.expectedReturn,
  }));

  return (
    <div className="h-72">
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <Pie
            data={data}
            cx="50%"
            cy="50%"
            innerRadius={60}
            outerRadius={100}
            paddingAngle={3}
            dataKey="value"
            label={({ name, value }) => `${name} (${value}%)`}
            labelLine={true}
          >
            {data.map((_, index) => (
              <Cell
                key={`cell-${index}`}
                fill={COLORS.funds[index % COLORS.funds.length]}
                stroke="white"
                strokeWidth={2}
              />
            ))}
          </Pie>
          <Tooltip
            content={({ active, payload }) => {
              if (active && payload && payload.length) {
                const data = payload[0].payload;
                return (
                  <div className="bg-white/95 backdrop-blur-sm border border-gray-200 rounded-lg shadow-xl p-3">
                    <p className="font-semibold text-gray-800">{data.fullName}</p>
                    <p className="text-sm text-gray-600">
                      Allocation: <strong>{data.value}%</strong>
                    </p>
                    <p className="text-sm text-gray-600">
                      Amount: <strong>{formatCurrency(data.amount)}</strong>
                    </p>
                    <p className="text-sm text-green-600">
                      Expected Return: <strong>{formatPercent(data.expectedReturn)}</strong>
                    </p>
                  </div>
                );
              }
              return null;
            }}
          />
        </PieChart>
      </ResponsiveContainer>
    </div>
  );
};

// Growth Projection Chart
const GrowthProjectionChart: React.FC<{ scenarios: Scenarios }> = ({ scenarios }) => {
  // Combine monthly projections for all scenarios
  const data = scenarios.expected.monthlyProjections.map((_, index) => ({
    month: index + 1,
    conservative: scenarios.conservative.monthlyProjections[index]?.value || 0,
    expected: scenarios.expected.monthlyProjections[index]?.value || 0,
    optimistic: scenarios.optimistic.monthlyProjections[index]?.value || 0,
  }));

  return (
    <div className="h-80">
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={data} margin={{ top: 10, right: 30, left: 20, bottom: 10 }}>
          <defs>
            <linearGradient id="conservativeGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%" stopColor={COLORS.conservative} stopOpacity={0.3} />
              <stop offset="95%" stopColor={COLORS.conservative} stopOpacity={0} />
            </linearGradient>
            <linearGradient id="expectedGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%" stopColor={COLORS.expected} stopOpacity={0.4} />
              <stop offset="95%" stopColor={COLORS.expected} stopOpacity={0} />
            </linearGradient>
            <linearGradient id="optimisticGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%" stopColor={COLORS.optimistic} stopOpacity={0.3} />
              <stop offset="95%" stopColor={COLORS.optimistic} stopOpacity={0} />
            </linearGradient>
          </defs>
          <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
          <XAxis
            dataKey="month"
            tick={{ fontSize: 12 }}
            tickFormatter={(value) => `M${value}`}
            stroke="#9ca3af"
          />
          <YAxis
            tick={{ fontSize: 12 }}
            tickFormatter={(value) => `${(value / 1000).toFixed(0)}K`}
            stroke="#9ca3af"
          />
          <Tooltip content={<CustomTooltip />} />
          <Legend
            wrapperStyle={{ paddingTop: '20px' }}
            iconType="circle"
          />
          <Area
            type="monotone"
            dataKey="optimistic"
            name="Optimistic"
            stroke={COLORS.optimistic}
            fill="url(#optimisticGrad)"
            strokeWidth={2}
          />
          <Area
            type="monotone"
            dataKey="expected"
            name="Expected"
            stroke={COLORS.expected}
            fill="url(#expectedGrad)"
            strokeWidth={3}
          />
          <Area
            type="monotone"
            dataKey="conservative"
            name="Conservative"
            stroke={COLORS.conservative}
            fill="url(#conservativeGrad)"
            strokeWidth={2}
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
};

// Strategy Comparison Chart
const StrategyComparisonChart: React.FC<{ comparison: StrategyComparison }> = ({ comparison }) => {
  const data = [
    {
      name: 'Lump Sum',
      investment: comparison.lumpSum.totalInvestment,
      projectedValue: comparison.lumpSum.projectedValue,
      returns: comparison.lumpSum.totalReturn,
    },
    {
      name: 'Monthly SIP',
      investment: comparison.monthlySip.totalInvestment,
      projectedValue: comparison.monthlySip.projectedValue,
      returns: comparison.monthlySip.totalReturn,
    },
  ];

  return (
    <div className="h-64">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} layout="vertical" margin={{ left: 20, right: 30 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
          <XAxis type="number" tickFormatter={(v) => `${(v / 1000).toFixed(0)}K`} />
          <YAxis type="category" dataKey="name" width={80} />
          <Tooltip
            content={({ active, payload }) => {
              if (active && payload && payload.length) {
                const data = payload[0].payload;
                return (
                  <div className="bg-white/95 backdrop-blur-sm border border-gray-200 rounded-lg shadow-xl p-3">
                    <p className="font-semibold text-gray-800">{data.name}</p>
                    <p className="text-sm text-gray-600">
                      Investment: {formatCurrency(data.investment)}
                    </p>
                    <p className="text-sm text-blue-600">
                      Projected: {formatCurrency(data.projectedValue)}
                    </p>
                    <p className="text-sm text-green-600">
                      Returns: +{formatCurrency(data.returns)}
                    </p>
                  </div>
                );
              }
              return null;
            }}
          />
          <Bar dataKey="projectedValue" name="Projected Value" fill={COLORS.expected} radius={[0, 4, 4, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
};

// Fund Details Table
const FundDetailsTable: React.FC<{ funds: FundRecommendation[] }> = ({ funds }) => (
  <div className="overflow-x-auto">
    <table className="w-full text-sm">
      <thead>
        <tr className="bg-gradient-to-r from-gray-50 to-gray-100">
          <th className="px-4 py-3 text-left font-semibold text-gray-700">Fund</th>
          <th className="px-4 py-3 text-center font-semibold text-gray-700">Type</th>
          <th className="px-4 py-3 text-right font-semibold text-gray-700">Allocation</th>
          <th className="px-4 py-3 text-right font-semibold text-gray-700">Investment</th>
          <th className="px-4 py-3 text-right font-semibold text-gray-700">Expected Return</th>
          <th className="px-4 py-3 text-center font-semibold text-gray-700">Shariah</th>
        </tr>
      </thead>
      <tbody>
        {funds.map((fund, index) => (
          <tr
            key={fund.fundCode}
            className={`border-b border-gray-100 hover:bg-blue-50/50 transition-colors ${
              index % 2 === 0 ? 'bg-white' : 'bg-gray-50/30'
            }`}
          >
            <td className="px-4 py-3">
              <div className="flex items-center gap-2">
                <div
                  className="w-3 h-3 rounded-full"
                  style={{ backgroundColor: COLORS.funds[index % COLORS.funds.length] }}
                />
                <div>
                  <div className="font-medium text-gray-900">{fund.fundCode}</div>
                  <div className="text-xs text-gray-500">{fund.fundName}</div>
                </div>
              </div>
            </td>
            <td className="px-4 py-3 text-center">
              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800">
                {fund.fundType}
              </span>
            </td>
            <td className="px-4 py-3 text-right font-medium text-gray-900">
              {formatPercent(fund.allocationPercent)}
            </td>
            <td className="px-4 py-3 text-right font-medium text-gray-900">
              {formatCurrency(fund.investmentAmount)}
            </td>
            <td className="px-4 py-3 text-right">
              <span className="text-green-600 font-semibold">
                {formatPercent(fund.expectedReturn)}
              </span>
            </td>
            <td className="px-4 py-3 text-center">
              {fund.isShariahCompliant ? (
                <span className="text-green-600 text-lg">✓</span>
              ) : (
                <span className="text-gray-300 text-lg">-</span>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  </div>
);

// Main Component
export const ProjectionResultCard: React.FC<ProjectionResultCardProps> = ({ result }) => {
  const [activeTab, setActiveTab] = useState<'overview' | 'growth' | 'funds' | 'strategy'>('overview');

  const { inputSummary, scenarios, recommendedFunds, callToAction, metadata, riskWarnings } = result;

  return (
    <div className="bg-white rounded-2xl shadow-xl border border-gray-200 overflow-hidden">
      {/* Header */}
      <div className="bg-gradient-to-r from-blue-600 via-indigo-600 to-purple-600 px-6 py-5 text-white">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <div className="w-14 h-14 bg-white/20 rounded-xl flex items-center justify-center backdrop-blur-sm">
              <span className="text-3xl">📈</span>
            </div>
            <div>
              <h2 className="text-2xl font-bold">Profit Projection</h2>
              <p className="text-blue-100 text-sm">احتساب الأرباح التقديرية</p>
            </div>
          </div>
          <div className="text-right">
            <div className="text-3xl font-bold">{formatCurrency(inputSummary.amount, inputSummary.currency)}</div>
            <div className="text-blue-100 text-sm">{inputSummary.horizon} • {inputSummary.riskProfile}</div>
          </div>
        </div>
      </div>

      {/* Quick Stats */}
      <div className="grid grid-cols-4 divide-x divide-gray-200 bg-gradient-to-b from-gray-50 to-white">
        <div className="p-4 text-center">
          <div className="text-2xl font-bold text-green-600">
            +{formatCurrency(scenarios.expected.totalReturn)}
          </div>
          <div className="text-xs text-gray-500 mt-1">Expected Return</div>
        </div>
        <div className="p-4 text-center">
          <div className="text-2xl font-bold text-blue-600">
            {formatPercent(scenarios.expected.annualizedReturn)}
          </div>
          <div className="text-xs text-gray-500 mt-1">Annual Return</div>
        </div>
        <div className="p-4 text-center">
          <div className="text-2xl font-bold text-purple-600">{recommendedFunds.length}</div>
          <div className="text-xs text-gray-500 mt-1">Recommended Funds</div>
        </div>
        <div className="p-4 text-center">
          <div className="text-2xl font-bold text-indigo-600">
            {formatCurrency(scenarios.expected.projectedValue)}
          </div>
          <div className="text-xs text-gray-500 mt-1">Projected Value</div>
        </div>
      </div>

      {/* Tabs */}
      <div className="border-b border-gray-200 px-6">
        <nav className="flex gap-6" aria-label="Tabs">
          {[
            { id: 'overview', label: 'Overview', icon: '📊' },
            { id: 'growth', label: 'Growth Chart', icon: '📈' },
            { id: 'funds', label: 'Fund Details', icon: '💼' },
            { id: 'strategy', label: 'Strategy', icon: '🎯' },
          ].map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id as any)}
              className={`flex items-center gap-2 py-4 border-b-2 font-medium text-sm transition-colors ${
                activeTab === tab.id
                  ? 'border-blue-500 text-blue-600'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
              }`}
            >
              <span>{tab.icon}</span>
              {tab.label}
            </button>
          ))}
        </nav>
      </div>

      {/* Tab Content */}
      <div className="p-6">
        {/* Overview Tab */}
        {activeTab === 'overview' && (
          <div className="space-y-6">
            <div className="grid grid-cols-3 gap-4">
              <ScenarioCard
                scenario={scenarios.conservative}
                colorClass="text-green-600"
                icon="🛡️"
              />
              <ScenarioCard
                scenario={scenarios.expected}
                colorClass="text-blue-600"
                icon="🎯"
                isHighlighted
              />
              <ScenarioCard
                scenario={scenarios.optimistic}
                colorClass="text-purple-600"
                icon="🚀"
              />
            </div>

            {/* Mini Chart Preview */}
            <div className="bg-gray-50 rounded-xl p-4">
              <h3 className="font-semibold text-gray-800 mb-3 flex items-center gap-2">
                <span>📈</span> Growth Trajectory
              </h3>
              <GrowthProjectionChart scenarios={scenarios} />
            </div>
          </div>
        )}

        {/* Growth Chart Tab */}
        {activeTab === 'growth' && (
          <div className="space-y-6">
            <div className="bg-gray-50 rounded-xl p-6">
              <h3 className="font-semibold text-gray-800 mb-4 text-lg">
                Investment Growth Over {inputSummary.horizon}
              </h3>
              <GrowthProjectionChart scenarios={scenarios} />
            </div>

            {/* Return Breakdown */}
            <div className="grid grid-cols-3 gap-4">
              {[
                { scenario: scenarios.conservative, color: 'green', label: 'Conservative' },
                { scenario: scenarios.expected, color: 'blue', label: 'Expected' },
                { scenario: scenarios.optimistic, color: 'purple', label: 'Optimistic' },
              ].map(({ scenario, color, label }) => (
                <div key={label} className={`bg-${color}-50 rounded-lg p-4 border border-${color}-100`}>
                  <div className="text-sm font-medium text-gray-600 mb-2">{label} Return Rate</div>
                  <div className={`text-2xl font-bold text-${color}-600`}>
                    {formatPercent(scenario.assumedReturnRate)}
                  </div>
                  <div className="text-xs text-gray-500 mt-1">Annual average</div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Funds Tab */}
        {activeTab === 'funds' && (
          <div className="space-y-6">
            <div className="grid grid-cols-2 gap-6">
              <div>
                <h3 className="font-semibold text-gray-800 mb-4 flex items-center gap-2">
                  <span>🥧</span> Allocation Breakdown
                </h3>
                <FundAllocationChart funds={recommendedFunds} />
              </div>
              <div>
                <h3 className="font-semibold text-gray-800 mb-4 flex items-center gap-2">
                  <span>💡</span> Why These Funds?
                </h3>
                <div className="space-y-3">
                  {recommendedFunds.slice(0, 3).map((fund) => (
                    <div key={fund.fundCode} className="bg-gray-50 rounded-lg p-3">
                      <div className="font-medium text-gray-900 text-sm">{fund.fundName}</div>
                      <ul className="mt-1 space-y-1">
                        {fund.reasons.slice(0, 2).map((reason, idx) => (
                          <li key={idx} className="text-xs text-gray-600 flex items-start gap-1">
                            <span className="text-green-500">✓</span>
                            {reason}
                          </li>
                        ))}
                      </ul>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            <FundDetailsTable funds={recommendedFunds} />
          </div>
        )}

        {/* Strategy Tab */}
        {activeTab === 'strategy' && scenarios.strategyComparison && (
          <div className="space-y-6">
            <div className="bg-gradient-to-br from-blue-50 to-indigo-50 rounded-xl p-6 border border-blue-100">
              <div className="flex items-center gap-3 mb-4">
                <span className="text-3xl">🏆</span>
                <div>
                  <h3 className="font-bold text-lg text-gray-900">
                    Recommended: {scenarios.strategyComparison.recommendedStrategy === 'LumpSum' ? 'Lump Sum Investment' : 'Monthly SIP'}
                  </h3>
                  <p className="text-sm text-gray-600">{scenarios.strategyComparison.recommendationRationale}</p>
                </div>
              </div>
            </div>

            <StrategyComparisonChart comparison={scenarios.strategyComparison} />

            <div className="grid grid-cols-2 gap-4">
              <div className={`p-5 rounded-xl border-2 ${
                scenarios.strategyComparison.recommendedStrategy === 'LumpSum'
                  ? 'border-blue-400 bg-blue-50'
                  : 'border-gray-200 bg-white'
              }`}>
                <div className="flex items-center justify-between mb-3">
                  <h4 className="font-semibold text-gray-900">💰 Lump Sum</h4>
                  {scenarios.strategyComparison.recommendedStrategy === 'LumpSum' && (
                    <span className="bg-blue-500 text-white text-xs px-2 py-1 rounded-full">Recommended</span>
                  )}
                </div>
                <div className="space-y-2">
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-600">Projected Value</span>
                    <span className="font-medium">{formatCurrency(scenarios.strategyComparison.lumpSum.projectedValue)}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-600">Total Return</span>
                    <span className="font-medium text-green-600">+{formatCurrency(scenarios.strategyComparison.lumpSum.totalReturn)}</span>
                  </div>
                </div>
                <p className="mt-3 text-xs text-gray-500">{scenarios.strategyComparison.lumpSum.benefit}</p>
              </div>

              <div className={`p-5 rounded-xl border-2 ${
                scenarios.strategyComparison.recommendedStrategy !== 'LumpSum'
                  ? 'border-blue-400 bg-blue-50'
                  : 'border-gray-200 bg-white'
              }`}>
                <div className="flex items-center justify-between mb-3">
                  <h4 className="font-semibold text-gray-900">📅 Monthly SIP</h4>
                  {scenarios.strategyComparison.recommendedStrategy !== 'LumpSum' && (
                    <span className="bg-blue-500 text-white text-xs px-2 py-1 rounded-full">Recommended</span>
                  )}
                </div>
                <div className="space-y-2">
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-600">Projected Value</span>
                    <span className="font-medium">{formatCurrency(scenarios.strategyComparison.monthlySip.projectedValue)}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-600">Total Return</span>
                    <span className="font-medium text-green-600">+{formatCurrency(scenarios.strategyComparison.monthlySip.totalReturn)}</span>
                  </div>
                </div>
                <p className="mt-3 text-xs text-gray-500">{scenarios.strategyComparison.monthlySip.benefit}</p>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Risk Warnings */}
      <div className="px-6 pb-4">
        <div className="bg-amber-50 border border-amber-200 rounded-lg p-4">
          <div className="flex items-start gap-2">
            <span className="text-amber-500 text-lg">⚠️</span>
            <div>
              <h4 className="font-medium text-amber-800 text-sm">Important Disclaimer</h4>
              <p className="text-xs text-amber-700 mt-1">{riskWarnings[0]}</p>
            </div>
          </div>
        </div>
      </div>

      {/* Call to Action */}
      <div className="bg-gradient-to-r from-gray-50 to-gray-100 px-6 py-5 border-t border-gray-200">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="text-xs text-gray-500">
              <div>Projection ID: <span className="font-mono">{result.projectionId}</span></div>
              <div>Generated: {new Date(metadata.createdAt).toLocaleString()}</div>
            </div>
          </div>
          <div className="flex gap-3">
            <button className="px-4 py-2 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-100 transition-colors text-sm font-medium">
              {callToAction.secondaryAction}
            </button>
            <button className="px-6 py-2 bg-gradient-to-r from-blue-600 to-indigo-600 text-white rounded-lg hover:from-blue-700 hover:to-indigo-700 transition-all shadow-md hover:shadow-lg text-sm font-medium">
              {callToAction.primaryAction} →
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ProjectionResultCard;
