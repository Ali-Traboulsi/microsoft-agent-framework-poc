import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import COLORS from "../../constants/colors";
import { StrategyComparison } from "../../interfaces/ProjectionResult.interface";
import { formatCurrency } from "../../utils/formatters";

// Strategy Comparison Chart
export const StrategyComparisonChart: React.FC<{ comparison: StrategyComparison }> = ({ comparison }) => {
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
          <CartesianGrid strokeDasharray="3 3" stroke="#374151" />
          <XAxis type="number" tickFormatter={(v) => `${(v / 1000).toFixed(0)}K`} tick={{ fill: '#9ca3af' }} stroke="#6b7280" />
          <YAxis type="category" dataKey="name" width={80} tick={{ fill: '#9ca3af' }} stroke="#6b7280" />
          <Tooltip
            content={({ active, payload }) => {
              if (active && payload && payload.length) {
                const data = payload[0].payload;
                return (
                  <div className="bg-gray-900/95 backdrop-blur-sm border border-gray-700 rounded-lg shadow-xl p-3">
                    <p className="font-semibold text-gray-100">{data.name}</p>
                    <p className="text-sm text-gray-300">
                      Investment: {formatCurrency(data.investment)}
                    </p>
                    <p className="text-sm text-blue-400">
                      Projected: {formatCurrency(data.projectedValue)}
                    </p>
                    <p className="text-sm text-emerald-400">
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

