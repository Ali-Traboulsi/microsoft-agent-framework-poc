/**
 * SignalR Connection Manager
 * Handles connection lifecycle for the Master Agent hub
 */

import * as signalR from '@microsoft/signalr';

const HUB_URL = '/hubs/master';

let connection: signalR.HubConnection | null = null;

// Handler registries for global event dispatch
type WorkflowProgressHandler = (event: WorkflowProgressEvent) => void;
type DelegationEventHandler = (conversationId: string, event: DelegationEvent) => void;

const workflowProgressHandlers = new Set<WorkflowProgressHandler>();
const delegationEventHandlers = new Set<DelegationEventHandler>();

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
 * Delegation event type (tool/sub-agent execution events)
 */
export interface DelegationEvent {
  type: string;
  content?: string | null;
  subAgentName?: string | null;
  toolName?: string | null;
  stepId?: string | null;
  stepName?: string | null;
  stepNameAr?: string | null;
  stepNumber?: number | null;
  totalSteps?: number | null;
  stepCompleted?: boolean;
  stepDurationMs?: number | null;
  stepDetails?: string | null;
  isComplete?: boolean;
  metadata?: Record<string, unknown> | null;
}

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
  // Check existing connection state
  const existingState = connection?.state;
  
  // If already connected, return existing connection
  if (existingState === signalR.HubConnectionState.Connected) {
    return connection!;
  }

  // If connecting in progress, wait for it to complete or fail
  if (existingState === signalR.HubConnectionState.Connecting) {
    // Wait for connection to complete (poll until state changes)
    for (let i = 0; i < 50; i++) { // Max 5 seconds
      await new Promise(resolve => setTimeout(resolve, 100));
      const currentState = connection?.state;
      if (!connection || currentState !== signalR.HubConnectionState.Connecting) {
        break;
      }
    }
    // If now connected, return it
    if (connection?.state === signalR.HubConnectionState.Connected) {
      return connection;
    }
  }

  // Build new connection
  const newConnection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL)
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // Retry with increasing delays
    .configureLogging(signalR.LogLevel.Information)
    .build();

  // Configure longer timeouts for external API calls (Render.com has cold starts)
  newConnection.serverTimeoutInMilliseconds = 300000; // 5 minutes
  newConnection.keepAliveIntervalInMilliseconds = 15000; // 15 seconds

  // Set the module-level connection BEFORE starting
  // This prevents race conditions with React StrictMode double-mount
  connection = newConnection;

  try {
    await newConnection.start();
    console.log('✅ Master Agent SignalR connected');
    
    // Register global handlers that dispatch to registered callbacks
    newConnection.on('receiveWorkflowProgress', (conversationId: string, event: unknown) => {
      console.log('🔔 receiveWorkflowProgress', { conversationId, event });
      // Dispatch to all registered handlers
      workflowProgressHandlers.forEach(handler => {
        try {
          handler(event as WorkflowProgressEvent);
        } catch (e) {
          console.error('Error in workflow progress handler:', e);
        }
      });
    });
    
    newConnection.on('receiveDelegationEvent', (conversationId: string, event: unknown) => {
      console.log('🔔 receiveDelegationEvent', { conversationId, event });
      // Dispatch to all registered handlers
      delegationEventHandlers.forEach(handler => {
        try {
          handler(conversationId, event as DelegationEvent);
        } catch (e) {
          console.error('Error in delegation event handler:', e);
        }
      });
    });
    
    return newConnection;
  } catch (error) {
    // Only clear connection if it's still the one we created
    if (connection === newConnection) {
      connection = null;
    }
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
 * Register a handler for real-time workflow progress events.
 * Handlers are stored in a registry and dispatched by the global SignalR handler.
 * 
 * @param handler The callback to invoke when a workflow progress event is received
 * @returns A cleanup function to unregister the handler
 */
export function onWorkflowProgress(
  handler: (event: WorkflowProgressEvent) => void
): () => void {
  workflowProgressHandlers.add(handler);
  console.log('✅ Registered workflow progress handler, total handlers:', workflowProgressHandlers.size);

  return () => {
    workflowProgressHandlers.delete(handler);
    console.log('🔌 Unregistered workflow progress handler, remaining:', workflowProgressHandlers.size);
  };
}

/**
 * Register a handler for real-time delegation events.
 * Handlers are stored in a registry and dispatched by the global SignalR handler.
 * 
 * @param handler The callback to invoke when a delegation event is received
 * @returns A cleanup function to unregister the handler
 */
export function onDelegationEvent(
  handler: (conversationId: string, event: DelegationEvent) => void
): () => void {
  delegationEventHandlers.add(handler);
  console.log('✅ Registered delegation event handler, total handlers:', delegationEventHandlers.size);

  return () => {
    delegationEventHandlers.delete(handler);
    console.log('🔌 Unregistered delegation event handler, remaining:', delegationEventHandlers.size);
  };
}
