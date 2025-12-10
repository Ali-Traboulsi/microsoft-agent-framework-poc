/**
 * SignalR Connection Manager
 * Handles connection lifecycle for the Master Agent hub
 */

import * as signalR from '@microsoft/signalr';

const HUB_URL = '/hubs/master';

let connection: signalR.HubConnection | null = null;

/**
 * Get or create the SignalR connection
 */
export function getConnection(): signalR.HubConnection | null {
  return connection;
}

/**
 * Check if currently connected
 */
export function isConnected(): boolean {
  return connection?.state === signalR.HubConnectionState.Connected;
}

/**
 * Get current connection state
 */
export function getConnectionState(): signalR.HubConnectionState | null {
  return connection?.state ?? null;
}

/**
 * Connect to the Master Agent SignalR hub
 */
export async function connect(): Promise<signalR.HubConnection> {
  if (connection?.state === signalR.HubConnectionState.Connected) {
    return connection;
  }

  connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL)
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // Retry with increasing delays
    .configureLogging(signalR.LogLevel.Information)
    .build();

  // Configure longer timeouts for external API calls (Render.com has cold starts)
  connection.serverTimeoutInMilliseconds = 300000; // 5 minutes
  connection.keepAliveIntervalInMilliseconds = 15000; // 15 seconds

  try {
    await connection.start();
    console.log('✅ Master Agent SignalR connected');
    
    // Register a global debug handler for workflow progress
    connection.on('receiveWorkflowProgress', (conversationId: string, event: any) => {
      console.log('🔔 GLOBAL DEBUG: receiveWorkflowProgress received', { conversationId, event });
    });
    
    return connection;
  } catch (error) {
    console.error('❌ Failed to connect to Master Agent hub:', error);
    throw error;
  }
}

/**
 * Disconnect from the SignalR hub
 */
export async function disconnect(): Promise<void> {
  if (connection) {
    await connection.stop();
    connection = null;
    console.log('🔌 Master Agent SignalR disconnected');
  }
}

/**
 * Ensure connection is established before streaming
 */
export async function ensureConnected(): Promise<signalR.HubConnection> {
  if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
    console.log('🔄 Not connected, attempting to connect...');
    return await connect();
  }
  return connection;
}

/**
 * Workflow progress event callback type
 */
export interface WorkflowProgressEvent {
  type: string;
  content?: string | null;
  stepId: string;
  stepName?: string | null;
  stepNameAr?: string | null;
  stepNumber: number;
  totalSteps: number;
  stepCompleted: boolean;
  stepDurationMs?: number | null;
  stepDetails?: string | null;
}

/**
 * Register a handler for real-time workflow progress events.
 * These events are pushed directly from the backend during tool execution,
 * bypassing the streaming loop for real-time updates.
 * 
 * @param handler The callback to invoke when a workflow progress event is received
 * @returns A cleanup function to unregister the handler
 */
export function onWorkflowProgress(
  handler: (event: WorkflowProgressEvent) => void
): () => void {
  if (!connection) {
    console.warn('⚠️ Cannot register workflow progress handler: connection is null');
    return () => {};
  }

  if (connection.state !== signalR.HubConnectionState.Connected) {
    console.warn('⚠️ Cannot register workflow progress handler: connection state is', connection.state);
    return () => {};
  }

  const eventHandler = (eventConversationId: string, event: WorkflowProgressEvent) => {
    // Accept all workflow progress events - the frontend has only one active conversation
    console.log('📊 Received workflow progress event:', event.stepId, event.stepCompleted, 'conversationId:', eventConversationId);
    handler(event);
  };

  // SignalR JS client uses camelCase for method names
  connection.on('receiveWorkflowProgress', eventHandler);
  console.log('✅ Registered workflow progress handler on connection state:', connection.state);

  return () => {
    if (connection) {
      connection.off('receiveWorkflowProgress', eventHandler);
      console.log('🔌 Unregistered workflow progress handler');
    }
  };
}
