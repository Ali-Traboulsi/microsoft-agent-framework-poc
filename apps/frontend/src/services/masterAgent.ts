/**
 * Master Agent Service
 * 
 * This file re-exports from the modular masterAgent directory.
 * The service has been split into:
 * - types.ts: All TypeScript interfaces
 * - connection.ts: SignalR connection management
 * - streaming.ts: Real-time streaming methods
 * - api.ts: REST API calls
 * - streamHandler.ts: Async generator utilities
 * - index.ts: Main exports
 */

export * from './masterAgent/index';

