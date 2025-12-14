/**
 * Master Agent Service
 * Main entry point - re-exports all functionality
 */

// Re-export all types
export type {
    CallToAction,
    ChatResponse,
    ContentInput,
    ConversationStats,
    FundRecommendation,
    MasterStreamResponse,
    MonthlyProjection,
    MultiModalResponse,
    ProjectionInputSummary,
    ProjectionMetadata,
    ProjectionResult,
    ProjectionScenario,
    ProjectionScenarios,
    StrategyComparison,
    StrategyDetails
} from './types';

// Re-export connection management
export {
    connect,
    disconnect,
    ensureConnected,
    getConnection,
    getConnectionState,
    isConnected,
    onDelegationEvent,
    onWorkflowProgress
} from './connection';

// Re-export types from connection
export type { DelegationEvent, WorkflowProgressEvent } from './connection';

// Re-export unified streaming method (single entry point for all chat types)
export { chatStreamUnified } from './streaming';

// Re-export types from streaming
export type { UnifiedChatRequest } from './streaming';

// Re-export REST API methods
export {
    chat,
    chatWithFiles,
    clearConversation,
    getConversationStats
} from './api';

