/**
 * Master Agent REST API Client
 * Non-streaming API calls for the master agent
 */

import type {
    ChatResponse,
    ConversationStats,
    MultiModalResponse,
} from './types';

const API_BASE = '/api/v2/MasterAgent';

/**
 * Non-streaming chat that returns structured projection results
 * Use this when you need access to projectionResult data
 */
export async function chat(
  message: string,
  conversationId: string,
  enableThinking: boolean = false
): Promise<ChatResponse> {
  const response = await fetch(`${API_BASE}/chat`, {
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
}

/**
 * Upload files with multimodal chat (non-streaming)
 */
export async function chatWithFiles(
  message: string,
  files: File[],
  conversationId?: string
): Promise<MultiModalResponse> {
  const formData = new FormData();
  formData.append('message', message);

  files.forEach((file) => {
    formData.append('files', file);
  });

  if (conversationId) {
    formData.append('conversationId', conversationId);
  }

  const response = await fetch(`${API_BASE}/chat/multimodal/upload`, {
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
export async function clearConversation(conversationId: string): Promise<boolean> {
  try {
    const response = await fetch(`${API_BASE}/conversation/${conversationId}`, {
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
export async function getConversationStats(): Promise<ConversationStats | null> {
  try {
    const response = await fetch(`${API_BASE}/conversation/stats`);

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
