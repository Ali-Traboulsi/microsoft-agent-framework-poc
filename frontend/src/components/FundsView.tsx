import React, { useEffect } from 'react';
import { fundsApi } from '../services/api';
import { useStore } from '../store/store';

export const FundsView: React.FC = () => {
  const { funds, setFunds, selectedFund, setSelectedFund } = useStore();

  useEffect(() => {
    loadFunds();
  }, []);

  const loadFunds = async () => {
    try {
      const response = await fundsApi.getAll();
      if (response.data.success && response.data.data) {
        setFunds(response.data.data);
      }
    } catch (error) {
      console.error('Failed to load funds:', error);
    }
  };

  const getRiskColor = (risk: number) => {
    // RiskLevel enum: 0=Low, 1=Medium, 2=High, 3=VeryHigh
    switch (risk) {
      case 0: // Low
        return 'bg-green-100 text-green-700';
      case 1: // Medium
        return 'bg-yellow-100 text-yellow-700';
      case 2: // High
        return 'bg-red-100 text-red-700';
      case 3: // VeryHigh
        return 'bg-red-200 text-red-800';
      default:
        return 'bg-gray-100 text-gray-700';
    }
  };

  const getRiskLabel = (risk: number) => {
    const labels = ['Low', 'Medium', 'High', 'Very High'];
    return labels[risk] || 'Unknown';
  };

  const formatNumber = (value: number | undefined | null, decimals: number = 2): string => {
    return value != null ? value.toFixed(decimals) : '0.00';
  };

  return (
    <div className="p-6 h-full overflow-y-auto">
      <div className="mb-6">
        <h2 className="text-2xl font-bold mb-2">Mutual Funds</h2>
        <p className="text-gray-600">Browse available investment funds</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {funds.map((fund) => (
          <div
            key={fund.fundId}
            onClick={() => setSelectedFund(fund)}
            className={`p-6 bg-white rounded-lg shadow cursor-pointer hover:shadow-lg transition-shadow border-2 ${
              selectedFund?.fundId === fund.fundId ? 'border-blue-500' : 'border-transparent'
            }`}
          >
            <div className="flex justify-between items-start mb-3">
              <div>
                <h3 className="font-bold text-lg">{fund.fundSymbol}</h3>
                <p className="text-sm text-gray-600">{fund.fundName}</p>
              </div>
              <span className={`px-2 py-1 rounded text-xs font-medium ${getRiskColor(fund.riskLevel)}`}>
                {getRiskLabel(fund.riskLevel)} Risk
              </span>
            </div>

            <div className="mb-3">
              <p className="text-2xl font-bold">${formatNumber(fund.nav)}</p>
              <p className="text-xs text-gray-500">NAV per unit</p>
            </div>

            <div className="space-y-2 mb-4">
              <div className="flex justify-between text-sm">
                <span className="text-gray-600">1Y Return:</span>
                <span className={`font-medium ${(fund.oneYearReturn ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                  {(fund.oneYearReturn ?? 0) >= 0 ? '+' : ''}{formatNumber(fund.oneYearReturn)}%
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-600">3Y Return:</span>
                <span className={`font-medium ${(fund.threeYearReturn ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                  {(fund.threeYearReturn ?? 0) >= 0 ? '+' : ''}{formatNumber(fund.threeYearReturn)}%
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-600">5Y Return:</span>
                <span className={`font-medium ${(fund.fiveYearReturn ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                  {(fund.fiveYearReturn ?? 0) >= 0 ? '+' : ''}{formatNumber(fund.fiveYearReturn)}%
                </span>
              </div>
            </div>

            <div className="pt-3 border-t">
              <div className="flex justify-between text-xs text-gray-500">
                <span>Category: {fund.category}</span>
                <span>ER: {formatNumber(fund.expenseRatio)}%</span>
              </div>
              <div className="text-xs text-gray-500 mt-1">
                Min: ${(fund.minInvestment ?? 0).toLocaleString()}
              </div>
            </div>
          </div>
        ))}

        {funds.length === 0 && (
          <div className="col-span-full text-center py-12 text-gray-500">
            <p className="text-lg">No funds found</p>
          </div>
        )}
      </div>

      {selectedFund && (
        <div className="mt-6 p-6 bg-white rounded-lg shadow">
          <h3 className="text-xl font-bold mb-4">Fund Details</h3>
          <div className="grid md:grid-cols-3 gap-6">
            <div>
              <h4 className="font-medium mb-2">Basic Information</h4>
              <dl className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <dt className="text-gray-600">Symbol:</dt>
                  <dd className="font-medium">{selectedFund.fundSymbol}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-gray-600">Category:</dt>
                  <dd className="font-medium">{selectedFund.category}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-gray-600">Risk:</dt>
                  <dd className={`font-medium ${getRiskColor(selectedFund.riskLevel)}`}>
                    {getRiskLabel(selectedFund.riskLevel)}
                  </dd>
                </div>
              </dl>
            </div>

            <div>
              <h4 className="font-medium mb-2">Performance</h4>
              <dl className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <dt className="text-gray-600">1 Year:</dt>
                  <dd className={`font-medium ${(selectedFund.oneYearReturn ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                    {(selectedFund.oneYearReturn ?? 0) >= 0 ? '+' : ''}{formatNumber(selectedFund.oneYearReturn)}%
                  </dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-gray-600">3 Years:</dt>
                  <dd className={`font-medium ${(selectedFund.threeYearReturn ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                    {(selectedFund.threeYearReturn ?? 0) >= 0 ? '+' : ''}{formatNumber(selectedFund.threeYearReturn)}%
                  </dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-gray-600">5 Years:</dt>
                  <dd className={`font-medium ${(selectedFund.fiveYearReturn ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                    {(selectedFund.fiveYearReturn ?? 0) >= 0 ? '+' : ''}{formatNumber(selectedFund.fiveYearReturn)}%
                  </dd>
                </div>
              </dl>
            </div>

            <div>
              <h4 className="font-medium mb-2">Costs & Requirements</h4>
              <dl className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <dt className="text-gray-600">NAV:</dt>
                  <dd className="font-medium">${formatNumber(selectedFund.nav)}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-gray-600">Expense Ratio:</dt>
                  <dd className="font-medium">{formatNumber(selectedFund.expenseRatio)}%</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-gray-600">Min Investment:</dt>
                  <dd className="font-medium">${(selectedFund.minInvestment ?? 0).toLocaleString()}</dd>
                </div>
              </dl>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
