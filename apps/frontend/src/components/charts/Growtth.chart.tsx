import { Area, AreaChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import COLORS from "../../constants/colors";
import { Scenarios } from "../../interfaces/ProjectionResult.interface";
import { formatCurrency } from "../../utils/formatters";


// Custom tooltip for charts - Dark theme
const CustomTooltip = ({ active, payload, label }: any) => {
  if (active && payload && payload.length) {
    return (
      <div className="bg-gray-900/95 backdrop-blur-sm border border-gray-700 rounded-lg shadow-xl p-3">
        <p className="font-semibold text-gray-100 mb-2">Month {label}</p>
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
          <CartesianGrid strokeDasharray="3 3" stroke="#374151" />
          <XAxis
            dataKey="month"
            tick={{ fontSize: 12, fill: '#9ca3af' }}
            tickFormatter={(value) => `M${value}`}
            stroke="#6b7280"
          />
          <YAxis
            tick={{ fontSize: 12, fill: '#9ca3af' }}
            tickFormatter={(value) => `${(value / 1000).toFixed(0)}K`}
            stroke="#6b7280"
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


export default GrowthProjectionChart;