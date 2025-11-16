import React, { useEffect } from 'react';
import { portfoliosApi } from '../services/api';
import { useStore } from '../store/store';

export const PortfolioView: React.FC = () => {
  const { portfolios, setPortfolios, selectedPortfolio, setSelectedPortfolio } = useStore();

  useEffect(() => {
    loadPortfolios();
  }, []);

  const loadPortfolios = async () => {
    try {
      const response = await portfoliosApi.getAll();
      console.log('Portfolios response:', response.data);
      if (response.data.success && response.data.data) {
        setPortfolios(response.data.data);
      }
    } catch (error) {
      console.error('Failed to load portfolios:', error);
    }
  };

  return (
    <div className="p-6">
      <div className="mb-6">
        <h2 className="text-2xl font-bold mb-2">Portfolios</h2>
        <p className="text-gray-600">View and manage investment portfolios</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {portfolios.map((portfolio) => (
          <div
            key={portfolio.portfolioId}
            onClick={() => setSelectedPortfolio(portfolio)}
            className={`p-6 bg-white rounded-lg shadow cursor-pointer hover:shadow-lg transition-shadow border-2 ${
              selectedPortfolio?.portfolioId === portfolio.portfolioId
                ? 'border-blue-500'
                : 'border-transparent'
            }`}
          >
            <div className="flex justify-between items-start mb-4">
              <div>
                <h3 className="font-bold text-lg">{portfolio.portfolioName}</h3>
                <p className="text-sm text-gray-500">{portfolio.portfolioId}</p>
              </div>
              <span className="px-2 py-1 bg-blue-100 text-blue-700 rounded text-xs font-medium">
                {portfolio.strategy}
              </span>
            </div>

            <div className="mb-4">
              <p className="text-2xl font-bold text-green-600">
                ${(portfolio.totalValue ?? 0).toLocaleString()}
              </p>
              <p className="text-xs text-gray-500">Total Value</p>
            </div>

            <div className="space-y-2">
              <div className="flex justify-between text-sm">
                <span className="text-gray-600">Holdings:</span>
                <span className="font-medium">{portfolio.holdings?.length ?? 0}</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-600">Created:</span>
                <span className="font-medium">
                  {new Date(portfolio.createdDate).toLocaleDateString()}
                </span>
              </div>
            </div>

            {portfolio.holdings && portfolio.holdings.length > 0 && (
              <div className="mt-4 pt-4 border-t">
                <p className="text-xs font-medium text-gray-500 mb-2">Top Holdings</p>
                {portfolio.holdings.slice(0, 3).map((holding, idx) => (
                  <div key={idx} className="flex justify-between text-xs mb-1">
                    <span>{holding.fundSymbol}</span>
                    <span className={(holding.gainLoss ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'}>
                      {(holding.gainLossPercentage ?? 0) >= 0 ? '+' : ''}
                      {(holding.gainLossPercentage ?? 0).toFixed(2)}%
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        ))}

        {portfolios.length === 0 && (
          <div className="col-span-full text-center py-12 text-gray-500">
            <p className="text-lg">No portfolios found</p>
            <p className="text-sm mt-2">Use the chat to create a new portfolio</p>
          </div>
        )}
      </div>

      {selectedPortfolio && (
        <div className="mt-6 p-6 bg-white rounded-lg shadow">
          <h3 className="text-xl font-bold mb-4">Portfolio Details</h3>
          <div className="grid md:grid-cols-2 gap-6">
            <div>
              <h4 className="font-medium mb-2">Information</h4>
              <dl className="space-y-2">
                <div className="flex justify-between text-sm">
                  <dt className="text-gray-600">Account ID:</dt>
                  <dd className="font-medium">{selectedPortfolio.accountId}</dd>
                </div>
                <div className="flex justify-between text-sm">
                  <dt className="text-gray-600">Strategy:</dt>
                  <dd className="font-medium">{selectedPortfolio.strategy}</dd>
                </div>
                <div className="flex justify-between text-sm">
                  <dt className="text-gray-600">Total Value:</dt>
                  <dd className="font-medium text-green-600">
                    ${selectedPortfolio.totalValue.toLocaleString()}
                  </dd>
                </div>
              </dl>
            </div>

            <div>
              <h4 className="font-medium mb-2">Holdings ({selectedPortfolio.holdings.length})</h4>
              <div className="space-y-2 max-h-48 overflow-y-auto">
                {selectedPortfolio.holdings.map((holding, idx) => (
                  <div key={idx} className="p-2 bg-gray-50 rounded">
                    <div className="flex justify-between items-center">
                      <div>
                        <p className="font-medium text-sm">{holding.fundSymbol}</p>
                        <p className="text-xs text-gray-600">{holding.shares} shares</p>
                      </div>
                      <div className="text-right">
                        <p className="font-medium text-sm">
                          ${holding.currentValue.toLocaleString()}
                        </p>
                        <p
                          className={`text-xs ${
                            holding.gainLoss >= 0 ? 'text-green-600' : 'text-red-600'
                          }`}
                        >
                          {holding.gainLoss >= 0 ? '+' : ''}
                          {holding.gainLossPercentage.toFixed(2)}%
                        </p>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
