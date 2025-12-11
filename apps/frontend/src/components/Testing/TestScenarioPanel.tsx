import React, { useState } from 'react';
import {
    allTestScenarios,
    getCategoryDisplayName,
    getScenariosByCategory,
    ScenarioCategory,
    TestScenario,
} from '../../constants/testScenarios';

interface TestScenarioPanelProps {
  onRunScenario: (messages: string[]) => void;
  onSendMessage?: (message: string) => void;
  isRunning?: boolean;
}

const categories: ScenarioCategory[] = [
  'intent_classification',
  'multi_turn_context',
  'multi_agent_coordination',
  'conflict_resolution',
  'chain_of_thought',
  'edge_cases',
  'stress_test',
];

export const TestScenarioPanel: React.FC<TestScenarioPanelProps> = ({
  onRunScenario,
  onSendMessage,
  isRunning = false,
}) => {
  const [selectedCategory, setSelectedCategory] = useState<ScenarioCategory>('intent_classification');
  const [expandedScenario, setExpandedScenario] = useState<string | null>(null);

  const scenarios = getScenariosByCategory(selectedCategory);

  const handleRunScenario = (scenario: TestScenario) => {
    const messages = scenario.messages
      .filter((m) => m.role === 'user')
      .map((m) => m.content);
    onRunScenario(messages);
  };

  const handleSendSingle = (message: string) => {
    if (onSendMessage) {
      onSendMessage(message);
    }
  };

  return (
    <div className="bg-gray-800 rounded-lg p-4 text-white">
      <h2 className="text-xl font-bold mb-4 flex items-center gap-2">
        🧪 Test Scenarios
        {isRunning && (
          <span className="text-sm text-yellow-400 animate-pulse">Running...</span>
        )}
      </h2>

      {/* Category Tabs */}
      <div className="flex flex-wrap gap-2 mb-4">
        {categories.map((cat) => (
          <button
            key={cat}
            onClick={() => setSelectedCategory(cat)}
            className={`px-3 py-1 rounded text-sm transition-colors ${
              selectedCategory === cat
                ? 'bg-blue-600 text-white'
                : 'bg-gray-700 text-gray-300 hover:bg-gray-600'
            }`}
          >
            {getCategoryDisplayName(cat)}
          </button>
        ))}
      </div>

      {/* Scenario Count */}
      <div className="text-sm text-gray-400 mb-3">
        {scenarios.length} scenario{scenarios.length !== 1 ? 's' : ''} in this category
      </div>

      {/* Scenario List */}
      <div className="space-y-2 max-h-96 overflow-y-auto">
        {scenarios.map((scenario) => (
          <div
            key={scenario.id}
            className="bg-gray-700 rounded-lg overflow-hidden"
          >
            {/* Scenario Header */}
            <div
              className="p-3 cursor-pointer hover:bg-gray-600 transition-colors"
              onClick={() =>
                setExpandedScenario(
                  expandedScenario === scenario.id ? null : scenario.id
                )
              }
            >
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="font-medium">{scenario.name}</h3>
                  <p className="text-sm text-gray-400">{scenario.description}</p>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-xs bg-gray-600 px-2 py-1 rounded">
                    {scenario.messages.filter((m) => m.role === 'user').length} message(s)
                  </span>
                  <span className="text-gray-400">
                    {expandedScenario === scenario.id ? '▼' : '▶'}
                  </span>
                </div>
              </div>
              
              {/* Tags */}
              <div className="flex flex-wrap gap-1 mt-2">
                {scenario.tags.map((tag) => (
                  <span
                    key={tag}
                    className="text-xs bg-blue-900 text-blue-200 px-2 py-0.5 rounded"
                  >
                    {tag}
                  </span>
                ))}
              </div>
            </div>

            {/* Expanded Details */}
            {expandedScenario === scenario.id && (
              <div className="border-t border-gray-600 p-3 bg-gray-750">
                {/* Messages */}
                <div className="mb-3">
                  <h4 className="text-sm font-medium text-gray-300 mb-2">
                    Messages:
                  </h4>
                  <div className="space-y-2">
                    {scenario.messages.map((msg, idx) => (
                      <div
                        key={idx}
                        className={`p-2 rounded text-sm ${
                          msg.role === 'user'
                            ? 'bg-blue-900 text-blue-100'
                            : 'bg-green-900 text-green-100'
                        }`}
                      >
                        <span className="font-medium">
                          {msg.role === 'user' ? '👤 User: ' : '🤖 Assistant: '}
                        </span>
                        {msg.content.length > 100
                          ? msg.content.substring(0, 100) + '...'
                          : msg.content}
                        {onSendMessage && msg.role === 'user' && (
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              handleSendSingle(msg.content);
                            }}
                            className="ml-2 text-xs bg-blue-600 hover:bg-blue-500 px-2 py-0.5 rounded"
                            disabled={isRunning}
                          >
                            Send
                          </button>
                        )}
                      </div>
                    ))}
                  </div>
                </div>

                {/* Expected Behavior */}
                <div className="mb-3">
                  <h4 className="text-sm font-medium text-gray-300 mb-2">
                    Expected Behavior:
                  </h4>
                  <div className="text-xs text-gray-400 space-y-1">
                    {scenario.expectedBehavior.intentType && (
                      <div>
                        <span className="text-gray-500">Intent:</span>{' '}
                        {scenario.expectedBehavior.intentType}
                      </div>
                    )}
                    {scenario.expectedBehavior.subAgentsInvolved && (
                      <div>
                        <span className="text-gray-500">Agents:</span>{' '}
                        {scenario.expectedBehavior.subAgentsInvolved.join(', ')}
                      </div>
                    )}
                    {scenario.expectedBehavior.keyFactsToDiscover && (
                      <div>
                        <span className="text-gray-500">Facts to discover:</span>{' '}
                        {scenario.expectedBehavior.keyFactsToDiscover.join(', ')}
                      </div>
                    )}
                    {scenario.expectedBehavior.shouldShowThinking && (
                      <div className="text-yellow-400">🧠 Should show thinking</div>
                    )}
                    {scenario.expectedBehavior.shouldShowCoordination && (
                      <div className="text-green-400">🤝 Should show coordination</div>
                    )}
                    {scenario.expectedBehavior.conflictsPossible && (
                      <div className="text-red-400">⚔️ Conflicts possible</div>
                    )}
                  </div>
                </div>

                {/* Run Button */}
                <button
                  onClick={() => handleRunScenario(scenario)}
                  disabled={isRunning}
                  className={`w-full py-2 rounded font-medium transition-colors ${
                    isRunning
                      ? 'bg-gray-600 text-gray-400 cursor-not-allowed'
                      : 'bg-green-600 hover:bg-green-500 text-white'
                  }`}
                >
                  {isRunning ? 'Running...' : '▶ Run Full Scenario'}
                </button>
              </div>
            )}
          </div>
        ))}
      </div>

      {/* Quick Stats */}
      <div className="mt-4 pt-4 border-t border-gray-700">
        <div className="text-sm text-gray-400">
          Total scenarios: {allTestScenarios.length}
        </div>
      </div>
    </div>
  );
};

export default TestScenarioPanel;
