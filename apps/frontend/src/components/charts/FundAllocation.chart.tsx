import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import COLORS from "../../constants/colors";
import { FundRecommendation } from "../../interfaces/ProjectionResult.interface";
import { formatCurrency, formatPercent } from "../../utils/formatters";

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
                stroke="#1f2937"
                strokeWidth={2}
              />
            ))}
          </Pie>
          <Tooltip
            content={({ active, payload }) => {
              if (active && payload && payload.length) {
                const data = payload[0].payload;
                return (
                  <div className="bg-gray-900/95 backdrop-blur-sm border border-gray-700 rounded-lg shadow-xl p-3">
                    <p className="font-semibold text-gray-100">{data.fullName}</p>
                    <p className="text-sm text-gray-300">
                      Allocation: <strong>{data.value}%</strong>
                    </p>
                    <p className="text-sm text-gray-300">
                      Amount: <strong>{formatCurrency(data.amount)}</strong>
                    </p>
                    <p className="text-sm text-emerald-400">
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

export default FundAllocationChart;