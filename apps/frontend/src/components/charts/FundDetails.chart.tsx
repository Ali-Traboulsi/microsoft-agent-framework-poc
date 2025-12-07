import COLORS from "../../constants/colors";
import { FundRecommendation } from "../../interfaces/ProjectionResult.interface";
import { formatCurrency, formatPercent } from "../../utils/formatters";

// Fund Details Table - Dark theme
export const FundDetailsTable: React.FC<{ funds: FundRecommendation[] }> = ({ funds }) => (
  <div className="overflow-x-auto">
    <table className="w-full text-sm">
      <thead>
        <tr className="bg-gradient-to-r from-gray-800 to-gray-900">
          <th className="px-4 py-3 text-left font-semibold text-gray-300">Fund</th>
          <th className="px-4 py-3 text-center font-semibold text-gray-300">Type</th>
          <th className="px-4 py-3 text-right font-semibold text-gray-300">Allocation</th>
          <th className="px-4 py-3 text-right font-semibold text-gray-300">Investment</th>
          <th className="px-4 py-3 text-right font-semibold text-gray-300">Expected Return</th>
          <th className="px-4 py-3 text-center font-semibold text-gray-300">Shariah</th>
        </tr>
      </thead>
      <tbody>
        {funds.map((fund, index) => (
          <tr
            key={fund.fundCode}
            className={`border-b border-gray-700 hover:bg-blue-900/30 transition-colors ${
              index % 2 === 0 ? 'bg-gray-800' : 'bg-gray-800/50'
            }`}
          >
            <td className="px-4 py-3">
              <div className="flex items-center gap-2">
                <div
                  className="w-3 h-3 rounded-full"
                  style={{ backgroundColor: COLORS.funds[index % COLORS.funds.length] }}
                />
                <div>
                  <div className="font-medium text-gray-100">{fund.fundCode}</div>
                  <div className="text-xs text-gray-500">{fund.fundName}</div>
                </div>
              </div>
            </td>
            <td className="px-4 py-3 text-center">
              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-900/50 text-blue-300">
                {fund.fundType}
              </span>
            </td>
            <td className="px-4 py-3 text-right font-medium text-gray-100">
              {formatPercent(fund.allocationPercent)}
            </td>
            <td className="px-4 py-3 text-right font-medium text-gray-100">
              {formatCurrency(fund.investmentAmount)}
            </td>
            <td className="px-4 py-3 text-right">
              <span className="text-emerald-400 font-semibold">
                {formatPercent(fund.expectedReturn)}
              </span>
            </td>
            <td className="px-4 py-3 text-center">
              {fund.isShariahCompliant ? (
                <span className="text-emerald-400 text-lg">✓</span>
              ) : (
                <span className="text-gray-600 text-lg">-</span>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  </div>
);

