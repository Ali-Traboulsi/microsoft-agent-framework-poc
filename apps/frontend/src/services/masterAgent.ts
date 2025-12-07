import * as signalR from '@microsoft/signalr';

interface MasterStreamResponse {
  type: string;
  content: string | null;
  subAgentName: string | null;
  toolName: string | null;
  isComplete: boolean;
  metadata: Record<string, any> | null;
  // Workflow progress fields
  stepId: string | null;
  stepName: string | null;
  stepNameAr: string | null;
  stepNumber: number | null;
  totalSteps: number | null;
  stepCompleted: boolean | null;
  stepDurationMs: number | null;
  stepDetails: string | null;
  // Projection result (included in Complete response for projection requests)
  projectionResult: ProjectionResult | null;
}

// Export the type for use in components
export type { MasterStreamResponse };

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

  async* chatStream(message: string, conversationId: string, enableThinking: boolean = false): AsyncGenerator<MasterStreamResponse> {
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
      conversationId,
      enableThinking
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

  async* chatStreamMultiModal(contents: ContentInput[], conversationId: string, enableThinking: boolean = false): AsyncGenerator<MasterStreamResponse> {
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
      ConversationId: conversationId,
      EnableThinking: enableThinking
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
   * Stream chat with thread persistence
   * Returns the thread ID as the first response (type: 'ThreadCreated')
   */
  async* chatStreamWithThread(
    message: string,
    threadId: string | null,
    conversationId: string | null,
    enableThinking: boolean = false
  ): AsyncGenerator<MasterStreamResponse & { threadId?: string }> {
    // Ensure we're connected before streaming
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      console.log('🔄 Not connected, attempting to connect...');
      await this.connect();
    }

    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('Failed to establish connection to Master Agent hub');
    }

    const stream = this.connection.stream<MasterStreamResponse>(
      'ChatStreamWithThread',
      message,
      threadId,
      conversationId,
      enableThinking
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

  /**
   * Clear conversation history
   */
  async clearConversation(conversationId: string): Promise<boolean> {
    try {
      const response = await fetch(`/api/v2/MasterAgent/conversation/${conversationId}`, {
        method: 'DELETE',
      });

      if (!response.ok) {
        console.error('Failed to clear conversation:', response.statusText);
        return false;
      }

      const result = await response.json();
      return result.data === true;
    } catch (error) {
      console.error('Error clearing conversation:', error);
      return false;
    }
  }

  /**
   * Get conversation statistics
   */
  async getConversationStats(): Promise<ConversationStats | null> {
    try {
      const response = await fetch('/api/v2/MasterAgent/conversation/stats');

      if (!response.ok) {
        console.error('Failed to get conversation stats:', response.statusText);
        return null;
      }

      const result = await response.json();
      return result.data;
    } catch (error) {
      console.error('Error getting conversation stats:', error);
      return null;
    }
  }

  /**
   * Non-streaming chat that returns structured projection results
   * Use this when you need access to projectionResult data
   */
  async chat(message: string, conversationId: string, enableThinking: boolean = false): Promise<ChatResponse> {
    try {
      const response = await fetch('/api/v2/MasterAgent/chat', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          message,
          conversationId,
          enableThinking,
        }),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to get response from Master Agent');
      }

      return response.json();
    } catch (error: any) {
      console.error('Error in chat:', error);
      throw error;
    }
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

export interface ConversationStats {
  activeConversations: number;
  conversationIds: string[];
}

// Projection Result Types for structured responses
export interface ProjectionResult {
  projectionId: string;
  inputSummary: {
    amount: number;
    currency: string;
    horizon: string;
    horizonAr: string;
    riskProfile: string;
    riskProfileAr: string;
    investmentType: string;
    shariahCompliant: boolean;
  };
  scenarios: {
    conservative: ProjectionScenario;
    expected: ProjectionScenario;
    optimistic: ProjectionScenario;
    strategyComparison?: StrategyComparison;
  };
  recommendedFunds: FundRecommendation[];
  riskWarnings: string[];
  riskWarningsAr: string[];
  callToAction: {
    primaryAction: string;
    primaryActionAr: string;
    link: string;
    secondaryAction: string;
    secondaryActionAr: string;
    secondaryLink: string;
  };
  metadata: {
    createdAt: string;
    expiresAt: string;
    workflowVersion: string;
    executionTimeMs: number;
    dataSources: string[];
    customerId: string | null;
    usedHistoricalData: boolean;
    usedMarketData: boolean;
  };
}

export interface ProjectionScenario {
  scenarioType: string;
  scenarioTypeAr: string;
  confidence: number;
  description: string;
  descriptionAr: string;
  assumedReturnRate: number;
  initialInvestment: number;
  projectedValue: number;
  totalReturn: number;
  annualizedReturn: number;
  monthlyProjections: MonthlyProjection[];
}

export interface MonthlyProjection {
  month: number;
  date: string;
  value: number;
  cumulativeReturn: number;
  monthlyContribution: number;
}

export interface StrategyComparison {
  lumpSum: {
    strategyName: string;
    totalInvestment: number;
    projectedValue: number;
    totalReturn: number;
    benefit: string;
    benefitAr: string;
  };
  monthlySip: {
    strategyName: string;
    totalInvestment: number;
    projectedValue: number;
    totalReturn: number;
    benefit: string;
    benefitAr: string;
  };
  recommendedStrategy: string;
  recommendationRationale: string;
  recommendationRationaleAr: string;
}

export interface FundRecommendation {
  fundCode: string;
  fundName: string;
  fundNameAr: string | null;
  fundType: string;
  allocationPercent: number;
  investmentAmount: number;
  expectedContribution: number;
  expectedReturn: number;
  reasons: string[];
  currentNav: number;
  isShariahCompliant: boolean;
}

export interface ChatResponse {
  success: boolean;
  data: {
    success: boolean;
    response: string;
    subAgentsUsed: string[];
    durationMs: number;
    errorMessage: string | null;
    projectionResult: ProjectionResult | null;
    hasProjectionResult: boolean;
  };
  error: string | null;
}

export const masterAgentService = new MasterAgentService();
