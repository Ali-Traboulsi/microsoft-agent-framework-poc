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
