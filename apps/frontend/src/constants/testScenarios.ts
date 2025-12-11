/**
 * AI Agent Test Scenarios
 * 
 * Comprehensive test scenarios to validate:
 * - Intent Classification Layer
 * - Structured Context Store  
 * - Chain-of-Thought Reasoning
 * - Sub-Agent Coordination Protocol
 * 
 * Usage: Import these scenarios into your chat interface for testing
 */

// ============================================
// Test Scenario Categories
// ============================================

export interface TestScenario {
  id: string;
  name: string;
  description: string;
  category: ScenarioCategory;
  messages: ConversationMessage[];
  expectedBehavior: ExpectedBehavior;
  tags: string[];
}

export interface ConversationMessage {
  role: 'user' | 'assistant';
  content: string;
  waitForResponse?: boolean;
  delayMs?: number;
}

export interface ExpectedBehavior {
  intentType?: string;
  subAgentsInvolved?: string[];
  shouldShowThinking?: boolean;
  shouldShowCoordination?: boolean;
  keyFactsToDiscover?: string[];
  conflictsPossible?: boolean;
}

export type ScenarioCategory = 
  | 'intent_classification'
  | 'multi_turn_context'
  | 'multi_agent_coordination'
  | 'conflict_resolution'
  | 'chain_of_thought'
  | 'edge_cases'
  | 'stress_test';

// ============================================
// Intent Classification Test Scenarios
// ============================================

export const intentClassificationScenarios: TestScenario[] = [
  {
    id: 'intent-1',
    name: 'Simple Portfolio Query',
    description: 'Test basic portfolio inquiry intent detection',
    category: 'intent_classification',
    messages: [
      { role: 'user', content: 'What is my current portfolio value?' }
    ],
    expectedBehavior: {
      intentType: 'PortfolioAnalysis',
      subAgentsInvolved: ['PortfolioManager'],
      shouldShowThinking: true
    },
    tags: ['portfolio', 'single-agent', 'simple']
  },
  {
    id: 'intent-2',
    name: 'Investment Recommendation Request',
    description: 'Test investment advice intent with risk tolerance',
    category: 'intent_classification',
    messages: [
      { role: 'user', content: 'I have $50,000 to invest and I\'m a moderate risk investor. What do you recommend?' }
    ],
    expectedBehavior: {
      intentType: 'InvestmentAdvice',
      subAgentsInvolved: ['InvestmentAdvisor', 'ComplianceOfficer'],
      shouldShowThinking: true,
      keyFactsToDiscover: ['investment_amount', 'risk_profile']
    },
    tags: ['investment', 'multi-agent', 'recommendation']
  },
  {
    id: 'intent-3',
    name: 'Account Balance Inquiry',
    description: 'Test account operations intent',
    category: 'intent_classification',
    messages: [
      { role: 'user', content: 'Check my account balance for account ACC001' }
    ],
    expectedBehavior: {
      intentType: 'AccountBalance',
      subAgentsInvolved: ['AccountServices'],
      keyFactsToDiscover: ['account_id']
    },
    tags: ['account', 'single-agent', 'simple']
  },
  {
    id: 'intent-4',
    name: 'Profit Projection Request',
    description: 'Test projection intent with time horizon',
    category: 'intent_classification',
    messages: [
      { role: 'user', content: 'Project my portfolio returns over the next 5 years with Monte Carlo simulation' }
    ],
    expectedBehavior: {
      intentType: 'ProfitProjection',
      subAgentsInvolved: ['ProfitProjection', 'PortfolioManager'],
      keyFactsToDiscover: ['projection_period', 'simulation_type']
    },
    tags: ['projection', 'multi-agent', 'complex']
  },
  {
    id: 'intent-5',
    name: 'Compliance Check',
    description: 'Test compliance verification intent',
    category: 'intent_classification',
    messages: [
      { role: 'user', content: 'Is my portfolio compliant with regulatory requirements?' }
    ],
    expectedBehavior: {
      intentType: 'ComplianceCheck',
      subAgentsInvolved: ['ComplianceOfficer', 'PortfolioManager']
    },
    tags: ['compliance', 'multi-agent', 'verification']
  },
  {
    id: 'intent-6',
    name: 'Ambiguous Request',
    description: 'Test how system handles ambiguous intents',
    category: 'intent_classification',
    messages: [
      { role: 'user', content: 'Can you help me with my investments?' }
    ],
    expectedBehavior: {
      intentType: 'GeneralInquiry',
      shouldShowThinking: true
    },
    tags: ['ambiguous', 'clarification-needed']
  }
];

// ============================================
// Multi-Turn Context Test Scenarios
// ============================================

export const multiTurnContextScenarios: TestScenario[] = [
  {
    id: 'context-1',
    name: 'Portfolio Deep Dive',
    description: 'Test context retention across multiple turns about portfolio',
    category: 'multi_turn_context',
    messages: [
      { role: 'user', content: 'Show me my portfolio holdings', waitForResponse: true },
      { role: 'user', content: 'What percentage is in technology stocks?', waitForResponse: true },
      { role: 'user', content: 'Should I rebalance based on current market conditions?' }
    ],
    expectedBehavior: {
      subAgentsInvolved: ['PortfolioManager', 'InvestmentAdvisor'],
      shouldShowThinking: true,
      keyFactsToDiscover: ['holdings', 'sector_allocation']
    },
    tags: ['multi-turn', 'context-aware', 'portfolio']
  },
  {
    id: 'context-2',
    name: 'Customer Data Lookup Flow',
    description: 'Test CIF-based lookup with follow-up questions',
    category: 'multi_turn_context',
    messages: [
      { role: 'user', content: 'Look up customer with CIF 123456789', waitForResponse: true },
      { role: 'user', content: 'What is their risk profile?', waitForResponse: true },
      { role: 'user', content: 'What investments would you recommend for them?' }
    ],
    expectedBehavior: {
      subAgentsInvolved: ['ExternalApiServices', 'AccountServices', 'InvestmentAdvisor'],
      keyFactsToDiscover: ['customer_name', 'cif', 'risk_profile']
    },
    tags: ['multi-turn', 'customer-data', 'recommendation']
  },
  {
    id: 'context-3',
    name: 'Pronoun Resolution',
    description: 'Test if system correctly resolves pronouns to previous context',
    category: 'multi_turn_context',
    messages: [
      { role: 'user', content: 'Tell me about the SNB Capital Equity Fund', waitForResponse: true },
      { role: 'user', content: 'What is its expense ratio?', waitForResponse: true },
      { role: 'user', content: 'Compare it with similar funds' }
    ],
    expectedBehavior: {
      keyFactsToDiscover: ['fund_name', 'expense_ratio']
    },
    tags: ['multi-turn', 'pronoun-resolution', 'funds']
  },
  {
    id: 'context-4',
    name: 'Topic Switching',
    description: 'Test context management when user switches topics',
    category: 'multi_turn_context',
    messages: [
      { role: 'user', content: 'Show my portfolio value', waitForResponse: true },
      { role: 'user', content: 'Actually, first check my account balance', waitForResponse: true },
      { role: 'user', content: 'Now back to the portfolio - what\'s my allocation?' }
    ],
    expectedBehavior: {
      subAgentsInvolved: ['PortfolioManager', 'AccountServices'],
      shouldShowThinking: true
    },
    tags: ['multi-turn', 'topic-switch', 'context-management']
  }
];

// ============================================
// Multi-Agent Coordination Test Scenarios
// ============================================

export const coordinationScenarios: TestScenario[] = [
  {
    id: 'coord-1',
    name: 'Complete Investment Workflow',
    description: 'Test full workflow: account → compliance → advisor → portfolio',
    category: 'multi_agent_coordination',
    messages: [
      { role: 'user', content: 'I want to invest $100,000 in mutual funds. I\'m a conservative investor. Please verify my account, check compliance, get recommendations, and create a portfolio.' }
    ],
    expectedBehavior: {
      intentType: 'CompleteInvestmentWorkflow',
      subAgentsInvolved: ['AccountServices', 'ComplianceOfficer', 'InvestmentAdvisor', 'PortfolioManager'],
      shouldShowCoordination: true,
      keyFactsToDiscover: ['account_status', 'compliance_status', 'recommendations', 'portfolio_id']
    },
    tags: ['workflow', 'all-agents', 'complex']
  },
  {
    id: 'coord-2',
    name: 'Risk Assessment with Multiple Perspectives',
    description: 'Test coordination between advisor and compliance for risk',
    category: 'multi_agent_coordination',
    messages: [
      { role: 'user', content: 'Assess the risk of adding high-yield bonds to my portfolio' }
    ],
    expectedBehavior: {
      subAgentsInvolved: ['InvestmentAdvisor', 'ComplianceOfficer', 'PortfolioManager'],
      shouldShowCoordination: true,
      conflictsPossible: true
    },
    tags: ['risk', 'multi-perspective', 'coordination']
  },
  {
    id: 'coord-3',
    name: 'Portfolio with Projection',
    description: 'Test portfolio analysis combined with future projection',
    category: 'multi_agent_coordination',
    messages: [
      { role: 'user', content: 'Analyze my current portfolio and project its growth for the next 3 years' }
    ],
    expectedBehavior: {
      intentType: 'PortfolioWithProjection',
      subAgentsInvolved: ['PortfolioManager', 'ProfitProjection'],
      shouldShowCoordination: true
    },
    tags: ['portfolio', 'projection', 'composite']
  },
  {
    id: 'coord-4',
    name: 'Investment Decision Pipeline',
    description: 'Test sequential fact sharing between agents',
    category: 'multi_agent_coordination',
    messages: [
      { role: 'user', content: 'Should I sell my tech stocks and buy bonds? Consider my risk profile and current market conditions.' }
    ],
    expectedBehavior: {
      subAgentsInvolved: ['PortfolioManager', 'InvestmentAdvisor', 'ComplianceOfficer'],
      shouldShowCoordination: true,
      keyFactsToDiscover: ['current_holdings', 'risk_profile', 'market_conditions', 'recommendation']
    },
    tags: ['decision', 'fact-sharing', 'multi-agent']
  }
];

// ============================================
// Conflict Resolution Test Scenarios
// ============================================

export const conflictResolutionScenarios: TestScenario[] = [
  {
    id: 'conflict-1',
    name: 'Advisor vs Compliance Conflict',
    description: 'Test when investment recommendation conflicts with compliance limits',
    category: 'conflict_resolution',
    messages: [
      { role: 'user', content: 'I want to put all my money into a single high-risk cryptocurrency fund' }
    ],
    expectedBehavior: {
      subAgentsInvolved: ['InvestmentAdvisor', 'ComplianceOfficer'],
      shouldShowCoordination: true,
      conflictsPossible: true
    },
    tags: ['conflict', 'compliance', 'risk']
  },
  {
    id: 'conflict-2',
    name: 'Conservative vs Aggressive Strategy',
    description: 'Test handling of conflicting investment strategies',
    category: 'conflict_resolution',
    messages: [
      { role: 'user', content: 'My advisor told me to be aggressive but my profile says conservative. What should I do?' }
    ],
    expectedBehavior: {
      shouldShowThinking: true,
      conflictsPossible: true
    },
    tags: ['conflict', 'strategy', 'clarification']
  }
];

// ============================================
// Chain-of-Thought Reasoning Test Scenarios
// ============================================

export const chainOfThoughtScenarios: TestScenario[] = [
  {
    id: 'cot-1',
    name: 'Complex Investment Decision',
    description: 'Test visible reasoning for complex financial decision',
    category: 'chain_of_thought',
    messages: [
      { role: 'user', content: 'I\'m 45 years old, have $500k saved, want to retire at 60 with $80k/year income. How should I invest?' }
    ],
    expectedBehavior: {
      shouldShowThinking: true,
      subAgentsInvolved: ['InvestmentAdvisor', 'ProfitProjection'],
      keyFactsToDiscover: ['age', 'savings', 'retirement_age', 'income_goal']
    },
    tags: ['reasoning', 'retirement', 'complex']
  },
  {
    id: 'cot-2',
    name: 'Market Analysis Reasoning',
    description: 'Test reasoning about market conditions',
    category: 'chain_of_thought',
    messages: [
      { role: 'user', content: 'Given current interest rates and inflation, should I move to bonds or stay in equities?' }
    ],
    expectedBehavior: {
      shouldShowThinking: true,
      subAgentsInvolved: ['InvestmentAdvisor']
    },
    tags: ['reasoning', 'market', 'analysis']
  },
  {
    id: 'cot-3',
    name: 'Step-by-Step Calculation',
    description: 'Test visible calculation reasoning',
    category: 'chain_of_thought',
    messages: [
      { role: 'user', content: 'If I invest $10,000 monthly for 20 years at 7% return, how much will I have?' }
    ],
    expectedBehavior: {
      shouldShowThinking: true,
      subAgentsInvolved: ['ProfitProjection']
    },
    tags: ['reasoning', 'calculation', 'projection']
  }
];

// ============================================
// Edge Cases Test Scenarios
// ============================================

export const edgeCaseScenarios: TestScenario[] = [
  {
    id: 'edge-1',
    name: 'Empty Request',
    description: 'Test handling of empty or whitespace-only input',
    category: 'edge_cases',
    messages: [
      { role: 'user', content: '   ' }
    ],
    expectedBehavior: {
      intentType: 'Unknown'
    },
    tags: ['edge-case', 'validation']
  },
  {
    id: 'edge-2',
    name: 'Non-Financial Request',
    description: 'Test handling of off-topic requests',
    category: 'edge_cases',
    messages: [
      { role: 'user', content: 'What\'s the weather like today?' }
    ],
    expectedBehavior: {
      intentType: 'GeneralInquiry'
    },
    tags: ['edge-case', 'off-topic']
  },
  {
    id: 'edge-3',
    name: 'Very Long Input',
    description: 'Test handling of extremely long messages',
    category: 'edge_cases',
    messages: [
      { role: 'user', content: 'I need help with my portfolio. ' + 'This is a very detailed request. '.repeat(50) + 'What should I do?' }
    ],
    expectedBehavior: {
      shouldShowThinking: true
    },
    tags: ['edge-case', 'long-input']
  },
  {
    id: 'edge-4',
    name: 'Multiple Questions in One',
    description: 'Test handling of compound requests',
    category: 'edge_cases',
    messages: [
      { role: 'user', content: 'What is my portfolio value? Also check my account balance. And what funds do you recommend? Plus show me a 5-year projection.' }
    ],
    expectedBehavior: {
      intentType: 'ComprehensiveAnalysis',
      subAgentsInvolved: ['PortfolioManager', 'AccountServices', 'InvestmentAdvisor', 'ProfitProjection'],
      shouldShowCoordination: true
    },
    tags: ['edge-case', 'compound', 'multi-agent']
  },
  {
    id: 'edge-5',
    name: 'Arabic Language Input',
    description: 'Test handling of Arabic language requests',
    category: 'edge_cases',
    messages: [
      { role: 'user', content: 'ما هي قيمة محفظتي الاستثمارية؟' }
    ],
    expectedBehavior: {
      intentType: 'PortfolioAnalysis'
    },
    tags: ['edge-case', 'arabic', 'language']
  },
  {
    id: 'edge-6',
    name: 'Invalid Account ID',
    description: 'Test error handling for non-existent accounts',
    category: 'edge_cases',
    messages: [
      { role: 'user', content: 'Show portfolio for account XYZ999999' }
    ],
    expectedBehavior: {
      subAgentsInvolved: ['AccountServices', 'PortfolioManager']
    },
    tags: ['edge-case', 'error-handling', 'validation']
  }
];

// ============================================
// Stress Test Scenarios
// ============================================

export const stressTestScenarios: TestScenario[] = [
  {
    id: 'stress-1',
    name: 'Rapid Fire Questions',
    description: 'Test system under rapid sequential requests',
    category: 'stress_test',
    messages: [
      { role: 'user', content: 'Portfolio value?', delayMs: 100 },
      { role: 'user', content: 'Account balance?', delayMs: 100 },
      { role: 'user', content: 'Fund recommendations?', delayMs: 100 }
    ],
    expectedBehavior: {
      shouldShowThinking: true
    },
    tags: ['stress', 'rapid-fire', 'performance']
  },
  {
    id: 'stress-2',
    name: 'Deep Conversation (10+ turns)',
    description: 'Test context retention over many turns',
    category: 'stress_test',
    messages: [
      { role: 'user', content: 'Show my portfolio', waitForResponse: true },
      { role: 'user', content: 'What\'s the largest holding?', waitForResponse: true },
      { role: 'user', content: 'How has it performed?', waitForResponse: true },
      { role: 'user', content: 'Should I keep it?', waitForResponse: true },
      { role: 'user', content: 'What would you replace it with?', waitForResponse: true },
      { role: 'user', content: 'Is that compliant?', waitForResponse: true },
      { role: 'user', content: 'Project the impact over 3 years', waitForResponse: true },
      { role: 'user', content: 'Compare with current allocation', waitForResponse: true },
      { role: 'user', content: 'Make the change', waitForResponse: true },
      { role: 'user', content: 'Confirm the new portfolio' }
    ],
    expectedBehavior: {
      subAgentsInvolved: ['PortfolioManager', 'InvestmentAdvisor', 'ComplianceOfficer', 'ProfitProjection'],
      shouldShowCoordination: true
    },
    tags: ['stress', 'long-conversation', 'context-retention']
  }
];

// ============================================
// All Scenarios Combined
// ============================================

export const allTestScenarios: TestScenario[] = [
  ...intentClassificationScenarios,
  ...multiTurnContextScenarios,
  ...coordinationScenarios,
  ...conflictResolutionScenarios,
  ...chainOfThoughtScenarios,
  ...edgeCaseScenarios,
  ...stressTestScenarios
];

// ============================================
// Scenario Runner Utilities
// ============================================

export const getScenariosByCategory = (category: ScenarioCategory): TestScenario[] => {
  return allTestScenarios.filter(s => s.category === category);
};

export const getScenariosByTag = (tag: string): TestScenario[] => {
  return allTestScenarios.filter(s => s.tags.includes(tag));
};

export const getScenarioById = (id: string): TestScenario | undefined => {
  return allTestScenarios.find(s => s.id === id);
};

export const getCategoryDisplayName = (category: ScenarioCategory): string => {
  const names: Record<ScenarioCategory, string> = {
    'intent_classification': '🎯 Intent Classification',
    'multi_turn_context': '🔄 Multi-Turn Context',
    'multi_agent_coordination': '🤝 Agent Coordination',
    'conflict_resolution': '⚔️ Conflict Resolution',
    'chain_of_thought': '🧠 Chain-of-Thought',
    'edge_cases': '🔧 Edge Cases',
    'stress_test': '⚡ Stress Tests'
  };
  return names[category];
};

// Quick access scenarios for common testing
export const quickTestScenarios = [
  getScenarioById('intent-1'),  // Simple portfolio query
  getScenarioById('context-1'), // Multi-turn context
  getScenarioById('coord-1'),   // Full workflow
  getScenarioById('cot-1'),     // Complex reasoning
].filter(Boolean) as TestScenario[];
