import React, { useState } from 'react';
import { ProjectionResultCardProps } from '../../../interfaces/ProjectionResult.interface';
import { formatCurrency, formatPercent } from '../../../utils/formatters';
import FundAllocationChart from '../../charts/FundAllocation.chart';
import { FundDetailsTable } from '../../charts/FundDetails.chart';
import GrowthProjectionChart from '../../charts/Growtth.chart';
import { StrategyComparisonChart } from '../../charts/StrategyComparision.chart';
import ScenarioCard from './ScenarioCard';


// Main Component
export const ProjectionResultCard: React.FC<ProjectionResultCardProps> = ({ result }) => {
  const [activeTab, setActiveTab] = useState<'overview' | 'growth' | 'funds' | 'strategy'>('overview');

  const { inputSummary, scenarios, recommendedFunds, callToAction, metadata, riskWarnings } = result;

  return (
    <div className="bg-gray-900 rounded-2xl shadow-xl border border-gray-700 overflow-hidden">
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
      <div className="grid grid-cols-4 divide-x divide-gray-700 bg-gradient-to-b from-gray-800 to-gray-900">
        <div className="p-4 text-center">
          <div className="text-2xl font-bold text-emerald-400">
            +{formatCurrency(scenarios.expected.totalReturn)}
          </div>
          <div className="text-xs text-gray-400 mt-1">Expected Return</div>
        </div>
        <div className="p-4 text-center">
          <div className="text-2xl font-bold text-blue-400">
            {formatPercent(scenarios.expected.annualizedReturn)}
          </div>
          <div className="text-xs text-gray-400 mt-1">Annual Return</div>
        </div>
        <div className="p-4 text-center">
          <div className="text-2xl font-bold text-purple-400">{recommendedFunds.length}</div>
          <div className="text-xs text-gray-400 mt-1">Recommended Funds</div>
        </div>
        <div className="p-4 text-center">
          <div className="text-2xl font-bold text-indigo-400">
            {formatCurrency(scenarios.expected.projectedValue)}
          </div>
          <div className="text-xs text-gray-400 mt-1">Projected Value</div>
        </div>
      </div>

      {/* Tabs */}
      <div className="border-b border-gray-700 px-6">
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
                  ? 'border-blue-500 text-blue-400'
                  : 'border-transparent text-gray-400 hover:text-gray-300 hover:border-gray-600'
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
                colorClass="text-emerald-400"
                icon="🛡️"
              />
              <ScenarioCard
                scenario={scenarios.expected}
                colorClass="text-blue-400"
                icon="🎯"
                isHighlighted
              />
              <ScenarioCard
                scenario={scenarios.optimistic}
                colorClass="text-purple-400"
                icon="🚀"
              />
            </div>

            {/* Mini Chart Preview */}
            <div className="bg-gray-800 rounded-xl p-4">
              <h3 className="font-semibold text-gray-200 mb-3 flex items-center gap-2">
                <span>📈</span> Growth Trajectory
              </h3>
              <GrowthProjectionChart scenarios={scenarios} />
            </div>
          </div>
        )}

        {/* Growth Chart Tab */}
        {activeTab === 'growth' && (
          <div className="space-y-6">
            <div className="bg-gray-800 rounded-xl p-6">
              <h3 className="font-semibold text-gray-200 mb-4 text-lg">
                Investment Growth Over {inputSummary.horizon}
              </h3>
              <GrowthProjectionChart scenarios={scenarios} />
            </div>

            {/* Return Breakdown */}
            <div className="grid grid-cols-3 gap-4">
              {[
                { scenario: scenarios.conservative, color: 'emerald', label: 'Conservative' },
                { scenario: scenarios.expected, color: 'blue', label: 'Expected' },
                { scenario: scenarios.optimistic, color: 'purple', label: 'Optimistic' },
              ].map(({ scenario, color, label }) => (
                <div key={label} className={`bg-${color}-900/30 rounded-lg p-4 border border-${color}-700/50`}>
                  <div className="text-sm font-medium text-gray-400 mb-2">{label} Return Rate</div>
                  <div className={`text-2xl font-bold text-${color}-400`}>
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
                <h3 className="font-semibold text-gray-200 mb-4 flex items-center gap-2">
                  <span>🥧</span> Allocation Breakdown
                </h3>
                <FundAllocationChart funds={recommendedFunds} />
              </div>
              <div>
                <h3 className="font-semibold text-gray-200 mb-4 flex items-center gap-2">
                  <span>💡</span> Why These Funds?
                </h3>
                <div className="space-y-3">
                  {recommendedFunds.slice(0, 3).map((fund) => (
                    <div key={fund.fundCode} className="bg-gray-800 rounded-lg p-3">
                      <div className="font-medium text-gray-100 text-sm">{fund.fundName}</div>
                      <ul className="mt-1 space-y-1">
                        {fund.reasons.slice(0, 2).map((reason, idx) => (
                          <li key={idx} className="text-xs text-gray-400 flex items-start gap-1">
                            <span className="text-emerald-400">✓</span>
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
            <div className="bg-gradient-to-br from-blue-900/40 to-indigo-900/40 rounded-xl p-6 border border-blue-700">
              <div className="flex items-center gap-3 mb-4">
                <span className="text-3xl">🏆</span>
                <div>
                  <h3 className="font-bold text-lg text-gray-100">
                    Recommended: {scenarios.strategyComparison.recommendedStrategy === 'LumpSum' ? 'Lump Sum Investment' : 'Monthly SIP'}
                  </h3>
                  <p className="text-sm text-gray-400">{scenarios.strategyComparison.recommendationRationale}</p>
                </div>
              </div>
            </div>

            <StrategyComparisonChart comparison={scenarios.strategyComparison} />

            <div className="grid grid-cols-2 gap-4">
              <div className={`p-5 rounded-xl border-2 ${
                scenarios.strategyComparison.recommendedStrategy === 'LumpSum'
                  ? 'border-blue-500 bg-blue-900/30'
                  : 'border-gray-700 bg-gray-800'
              }`}>
                <div className="flex items-center justify-between mb-3">
                  <h4 className="font-semibold text-gray-100">💰 Lump Sum</h4>
                  {scenarios.strategyComparison.recommendedStrategy === 'LumpSum' && (
                    <span className="bg-blue-500 text-white text-xs px-2 py-1 rounded-full">Recommended</span>
                  )}
                </div>
                <div className="space-y-2">
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-400">Projected Value</span>
                    <span className="font-medium text-gray-100">{formatCurrency(scenarios.strategyComparison.lumpSum.projectedValue)}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-400">Total Return</span>
                    <span className="font-medium text-emerald-400">+{formatCurrency(scenarios.strategyComparison.lumpSum.totalReturn)}</span>
                  </div>
                </div>
                <p className="mt-3 text-xs text-gray-500">{scenarios.strategyComparison.lumpSum.benefit}</p>
              </div>

              <div className={`p-5 rounded-xl border-2 ${
                scenarios.strategyComparison.recommendedStrategy !== 'LumpSum'
                  ? 'border-blue-500 bg-blue-900/30'
                  : 'border-gray-700 bg-gray-800'
              }`}>
                <div className="flex items-center justify-between mb-3">
                  <h4 className="font-semibold text-gray-100">📅 Monthly SIP</h4>
                  {scenarios.strategyComparison.recommendedStrategy !== 'LumpSum' && (
                    <span className="bg-blue-500 text-white text-xs px-2 py-1 rounded-full">Recommended</span>
                  )}
                </div>
                <div className="space-y-2">
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-400">Projected Value</span>
                    <span className="font-medium text-gray-100">{formatCurrency(scenarios.strategyComparison.monthlySip.projectedValue)}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-400">Total Return</span>
                    <span className="font-medium text-emerald-400">+{formatCurrency(scenarios.strategyComparison.monthlySip.totalReturn)}</span>
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
        <div className="bg-amber-900/30 border border-amber-700 rounded-lg p-4">
          <div className="flex items-start gap-2">
            <span className="text-amber-400 text-lg">⚠️</span>
            <div>
              <h4 className="font-medium text-amber-300 text-sm">Important Disclaimer</h4>
              <p className="text-xs text-amber-400 mt-1">{riskWarnings[0]}</p>
            </div>
          </div>
        </div>
      </div>

      {/* Call to Action */}
      <div className="bg-gradient-to-r from-gray-800 to-gray-900 px-6 py-5 border-t border-gray-700">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="text-xs text-gray-400">
              <div>Projection ID: <span className="font-mono text-gray-300">{result.projectionId}</span></div>
              <div>Generated: {new Date(metadata.createdAt).toLocaleString()}</div>
            </div>
          </div>
          <div className="flex gap-3">
            <button className="px-4 py-2 border border-gray-600 text-gray-300 rounded-lg hover:bg-gray-700 transition-colors text-sm font-medium">
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
