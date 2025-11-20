import * as signalR from '@microsoft/signalr';

interface MasterStreamResponse {
  type: string;
  content: string | null;
  subAgentName: string | null;
  toolName: string | null;
  isComplete: boolean;
  metadata: Record<string, any> | null;
}

interface ContentInput {
  Type: 'text' | 'image' | 'audio' | 'uri' | 'file';
  Text?: string;
  Data?: string;
  Uri?: string;
  MediaType?: string;
  FileName?: string;
}

class MasterAgentService {
  private connection: signalR.HubConnection | null = null;

  async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/master')  // Use Vite proxy instead of direct backend URL
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    try {
      await this.connection.start();
      console.log('✅ Master Agent SignalR connected');
    } catch (error) {
      console.error('❌ Failed to connect to Master Agent hub:', error);
      throw error;
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      console.log('🔌 Master Agent SignalR disconnected');
    }
  }

  async* chatStream(message: string, conversationId: string): AsyncGenerator<MasterStreamResponse> {
    // Ensure we're connected before streaming
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      console.log('🔄 Not connected, attempting to connect...');
      await this.connect();
    }

    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('Failed to establish connection to Master Agent hub');
    }

    const stream = this.connection.stream<MasterStreamResponse>(
      'ChatStream',
      message,
      conversationId
    );

    // Create a promise-based queue for async iteration
    const queue: MasterStreamResponse[] = [];
    let resolveNext: ((value: IteratorResult<MasterStreamResponse>) => void) | null = null;
    let error: Error | null = null;
    let completed = false;

    const subscription = stream.subscribe({
      next: (item) => {
        if (resolveNext) {
          resolveNext({ value: item, done: false });
          resolveNext = null;
        } else {
          queue.push(item);
        }
      },
      error: (err) => {
        error = err;
        if (resolveNext) {
          resolveNext({ value: undefined as any, done: true });
          resolveNext = null;
        }
      },
      complete: () => {
        completed = true;
        if (resolveNext) {
          resolveNext({ value: undefined as any, done: true });
          resolveNext = null;
        }
      }
    });

    try {
      while (!completed && !error) {
        if (queue.length > 0) {
          yield queue.shift()!;
        } else {
          const result = await new Promise<IteratorResult<MasterStreamResponse>>((resolve) => {
            resolveNext = resolve;
          });
          
          if (result.done) {
            break;
          }
          
          yield result.value;
        }
      }

      if (error) {
        throw error;
      }
    } finally {
      subscription.dispose();
    }
  }

  async* chatStreamMultiModal(contents: ContentInput[], conversationId: string): AsyncGenerator<MasterStreamResponse> {
    // Ensure we're connected before streaming
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      console.log('🔄 Not connected, attempting to connect...');
      await this.connect();
    }

    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('Failed to establish connection to Master Agent hub');
    }

    const request = {
      Message: contents.find(c => c.Type === 'text')?.Text || '',
      Contents: contents,
      ConversationId: conversationId
    };

    console.log('📤 Sending multimodal request:', JSON.stringify(request, null, 2));

    const stream = this.connection.stream<MasterStreamResponse>(
      'ChatStreamMultiModal',
      request
    );

    // Create a promise-based queue for async iteration
    const queue: MasterStreamResponse[] = [];
    let resolveNext: ((value: IteratorResult<MasterStreamResponse>) => void) | null = null;
    let error: Error | null = null;
    let completed = false;

    const subscription = stream.subscribe({
      next: (item) => {
        console.log('📥 Received chunk:', item);
        if (resolveNext) {
          resolveNext({ value: item, done: false });
          resolveNext = null;
        } else {
          queue.push(item);
        }
      },
      error: (err) => {
        console.error('❌ Stream error:', err);
        error = err;
        if (resolveNext) {
          resolveNext({ value: undefined as any, done: true });
          resolveNext = null;
        }
      },
      complete: () => {
        console.log('✅ Stream completed');
        completed = true;
        if (resolveNext) {
          resolveNext({ value: undefined as any, done: true });
          resolveNext = null;
        }
      }
    });

    try {
      while (!completed && !error) {
        if (queue.length > 0) {
          yield queue.shift()!;
        } else {
          const result = await new Promise<IteratorResult<MasterStreamResponse>>((resolve) => {
            resolveNext = resolve;
          });
          
          if (result.done) {
            break;
          }
          
          yield result.value;
        }
      }

      if (error) {
        throw error;
      }
    } finally {
      subscription.dispose();
    }
  }

  getConnectionState(): signalR.HubConnectionState | null {
    return this.connection?.state || null;
  }

  /**
   * Upload files with multimodal chat (non-streaming)
   */
  async chatWithFiles(
    message: string,
    files: File[],
    conversationId?: string
  ): Promise<MultiModalResponse> {
    const formData = new FormData();
    formData.append('message', message);
    
    files.forEach(file => {
      formData.append('files', file);
    });

    if (conversationId) {
      formData.append('conversationId', conversationId);
    }

    const response = await fetch('/api/v2/MasterAgent/chat/multimodal/upload', {
      method: 'POST',
      body: formData,
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || 'Failed to upload files');
    }

    return response.json();
  }
}

export interface MultiModalResponse {
  success: boolean;
  data: {
    message: string;
    timestamp: string;
    processingTimeMs: number;
    contentTypesProcessed: string[];
    conversationId: string;
  };
  error: string | null;
}

export const masterAgentService = new MasterAgentService();
