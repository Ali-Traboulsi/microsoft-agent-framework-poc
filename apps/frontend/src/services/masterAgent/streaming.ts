/**
 * Master Agent Streaming Methods
 * SignalR streaming implementations for real-time chat
 */

import { ensureConnected } from './connection';
import { createStreamGenerator } from './streamHandler';
import type { ContentInput, MasterStreamResponse } from './types';

/**
 * Unified request for all chat types - text, multimodal, or both.
 * Use this for consistent thread-based persistence and memory.
 */
export interface UnifiedChatRequest {
  /** Optional text message */
  message?: string;
  /** Optional list of multimodal content (images, audio, documents) */
  contents?: ContentInput[];
  /** Optional thread ID for continuing an existing conversation */
  threadId?: string | null;
  /** Optional conversation ID for memory/context tracking */
  conversationId?: string | null;
  /** Enable extended reasoning mode */
  enableThinking?: boolean;
}

/**
 * Unified chat streaming that handles both text and multimodal content
 * with consistent thread-based persistence and memory.
 * 
 * @param request - Unified request containing text and/or multimodal content
 * @returns Async generator of streaming responses
 */
export async function* chatStreamUnified(
  request: UnifiedChatRequest
): AsyncGenerator<MasterStreamResponse & { threadId?: string }> {
  const connection = await ensureConnected();

  if (!connection) {
    throw new Error('Failed to establish connection to Master Agent hub');
  }

  // Build request in PascalCase for C# backend
  const backendRequest = {
    Message: request.message || null,
    Contents: request.contents || null,
    ThreadId: request.threadId || null,
    ConversationId: request.conversationId || null,
    EnableThinking: request.enableThinking || false,
  };

  console.log('📤 Sending unified chat request:', JSON.stringify(backendRequest, null, 2));

  const stream = connection.stream<MasterStreamResponse>(
    'ChatStreamUnified',
    backendRequest
  );

  for await (const item of createStreamGenerator(stream)) {
    console.log('📥 Received chunk:', item);
    yield item;
  }
}
