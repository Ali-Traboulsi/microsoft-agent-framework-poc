import { Scenario } from "../../../interfaces/ProjectionResult.interface";
import { formatCurrency, formatPercent } from "../../../utils/formatters";

// Scenario Card Component - Dark theme
const ScenarioCard: React.FC<{
  scenario: Scenario;
  colorClass: string;
  icon: string;
  isHighlighted?: boolean;
}> = ({ scenario, colorClass, icon, isHighlighted }) => (
  <div
    className={`relative overflow-hidden rounded-xl border-2 p-5 transition-all duration-300 hover:shadow-lg ${
      isHighlighted
        ? 'border-blue-500 bg-gradient-to-br from-blue-900/50 to-indigo-900/50 shadow-md'
        : 'border-gray-700 bg-gray-800 hover:border-gray-600'
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
        <p className="text-xs text-gray-400">{scenario.confidence}% Confidence</p>
      </div>
    </div>

    <div className="space-y-3">
      <div className="flex justify-between items-center py-2 border-b border-gray-700">
        <span className="text-gray-400 text-sm">Projected Value</span>
        <span className="font-bold text-xl text-gray-100">
          {formatCurrency(scenario.projectedValue)}
        </span>
      </div>
      <div className="flex justify-between items-center py-2 border-b border-gray-700">
        <span className="text-gray-400 text-sm">Total Return</span>
        <span className={`font-semibold text-lg ${colorClass}`}>
          +{formatCurrency(scenario.totalReturn)}
        </span>
      </div>
      <div className="flex justify-between items-center py-2">
        <span className="text-gray-400 text-sm">Annualized Return</span>
        <span className={`font-bold text-lg ${colorClass}`}>
          {formatPercent(scenario.annualizedReturn)}
        </span>
      </div>
    </div>

    <p className="mt-4 text-xs text-gray-500 italic leading-relaxed">{scenario.description}</p>
  </div>
);

export default ScenarioCard;