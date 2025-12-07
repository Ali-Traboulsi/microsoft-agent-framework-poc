/**
 * Master Agent Streaming Methods
 * SignalR streaming implementations for real-time chat
 */

import { ensureConnected } from './connection';
import { createStreamGenerator } from './streamHandler';
import type { ContentInput, MasterStreamResponse } from './types';

/**
 * Stream chat messages from the master agent
 */
export async function* chatStream(
  message: string,
  conversationId: string,
  enableThinking: boolean = false
): AsyncGenerator<MasterStreamResponse> {
  const connection = await ensureConnected();

  if (!connection) {
    throw new Error('Failed to establish connection to Master Agent hub');
  }

  const stream = connection.stream<MasterStreamResponse>(
    'ChatStream',
    message,
    conversationId,
    enableThinking
  );

  yield* createStreamGenerator(stream);
}

/**
 * Stream multimodal chat (text, images, audio, files)
 */
export async function* chatStreamMultiModal(
  contents: ContentInput[],
  conversationId: string,
  enableThinking: boolean = false
): AsyncGenerator<MasterStreamResponse> {
  const connection = await ensureConnected();

  if (!connection) {
    throw new Error('Failed to establish connection to Master Agent hub');
  }

  const request = {
    Message: contents.find((c) => c.Type === 'text')?.Text || '',
    Contents: contents,
    ConversationId: conversationId,
    EnableThinking: enableThinking,
  };

  console.log('📤 Sending multimodal request:', JSON.stringify(request, null, 2));

  const stream = connection.stream<MasterStreamResponse>(
    'ChatStreamMultiModal',
    request
  );

  // Wrap with logging
  for await (const item of createStreamGenerator(stream)) {
    console.log('📥 Received chunk:', item);
    yield item;
  }
}

/**
 * Stream chat with thread persistence
 * Returns the thread ID as the first response (type: 'ThreadCreated')
 */
export async function* chatStreamWithThread(
  message: string,
  threadId: string | null,
  conversationId: string | null,
  enableThinking: boolean = false
): AsyncGenerator<MasterStreamResponse & { threadId?: string }> {
  const connection = await ensureConnected();

  if (!connection) {
    throw new Error('Failed to establish connection to Master Agent hub');
  }

  const stream = connection.stream<MasterStreamResponse>(
    'ChatStreamWithThread',
    message,
    threadId,
    conversationId,
    enableThinking
  );

  yield* createStreamGenerator(stream);
}
