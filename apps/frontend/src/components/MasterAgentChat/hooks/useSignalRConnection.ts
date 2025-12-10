import { useEffect, useState } from 'react';
import { connect, disconnect } from '../../../services/masterAgent';

export interface SignalRConnectionState {
  isConnected: boolean;
  isConnecting: boolean;
  connectionError: string | null;
  clearError: () => void;
}

export function useSignalRConnection(): SignalRConnectionState {
  const [isConnected, setIsConnected] = useState(false);
  const [isConnecting, setIsConnecting] = useState(false);
  const [connectionError, setConnectionError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const establishConnection = async () => {
      setIsConnecting(true);
      try {
        await connect();
        if (!cancelled) {
          setConnectionError(null);
          setIsConnected(true);
        }
      } catch (error) {
        if (!cancelled) {
          console.error('SignalR connection error:', error);
          const errorMessage =
            error instanceof Error ? error.message : 'Failed to connect to Master Agent';
          setConnectionError(errorMessage);
          setIsConnected(false);
        }
      } finally {
        if (!cancelled) {
          setIsConnecting(false);
        }
      }
    };

    establishConnection();

    return () => {
      cancelled = true;
      setTimeout(() => {
        disconnect().catch(console.error);
      }, 100);
      setIsConnected(false);
    };
  }, []);

  const clearError = () => setConnectionError(null);

  return {
    isConnected,
    isConnecting,
    connectionError,
    clearError,
  };
}
