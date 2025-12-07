import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

export interface StreamingMessage {
  message: string;
  agentName: string;
  isComplete: boolean;
}

class SignalRService {
  private connection: HubConnection | null = null;

  async connect(): Promise<void> {
    if (this.connection?.state === 'Connected') {
      return;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/agent')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    try {
      await this.connection.start();
      console.log('SignalR Connected');
    } catch (error) {
      console.error('SignalR connection failed:', error);
      throw new Error('Failed to connect to chat server. Please ensure the backend API is running on http://localhost:5000');
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      console.log('SignalR Disconnected');
    }
  }

  async *chatStream(message: string, agentName: string): AsyncGenerator<StreamingMessage> {
    if (!this.connection) {
      throw new Error('Not connected to SignalR hub');
    }

    const stream = this.connection.stream<StreamingMessage>('ChatStream', message, agentName);

    // Create a promise-based iterator for SignalR streams
    const buffer: StreamingMessage[] = [];
    let resolveNext: ((value: IteratorResult<StreamingMessage>) => void) | null = null;
    let error: Error | null = null;
    let isComplete = false;

    const subscription = stream.subscribe({
      next: (item: StreamingMessage) => {
        if (resolveNext) {
          resolveNext({ value: item, done: false });
          resolveNext = null;
        } else {
          buffer.push(item);
        }
      },
      error: (err: Error) => {
        error = err;
        if (resolveNext) {
          resolveNext({ value: undefined as any, done: true });
          resolveNext = null;
        }
      },
      complete: () => {
        isComplete = true;
        if (resolveNext) {
          resolveNext({ value: undefined as any, done: true });
          resolveNext = null;
        }
      },
    });

    try {
      while (true) {
        if (buffer.length > 0) {
          yield buffer.shift()!;
        } else if (error) {
          throw error;
        } else if (isComplete) {
          break;
        } else {
          const result = await new Promise<IteratorResult<StreamingMessage>>((resolve) => {
            resolveNext = resolve;
          });
          
          if (result.done) {
            if (error) throw error;
            break;
          }
          yield result.value;
        }
      }
    } finally {
      subscription.dispose();
    }
  }

  onReconnected(callback: (connectionId: string | undefined) => void): void {
    this.connection?.onreconnected(callback);
  }

  onReconnecting(callback: (error: Error | undefined) => void): void {
    this.connection?.onreconnecting(callback);
  }

  onClose(callback: (error: Error | undefined) => void): void {
    this.connection?.onclose(callback);
  }
}

export const signalRService = new SignalRService();
