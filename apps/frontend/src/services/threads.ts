const API_BASE = '/api';

// ============================================================================
// Types
// ============================================================================

export interface ThreadMessage {
  id: string;
  role: string; // 'user' | 'assistant' | 'system' | 'tool' (lowercase from backend)
  content: string;
  subAgentName?: string;
  toolCalls?: ToolCallInfo[];
  metadata?: Record<string, unknown>;
  timestamp: string;
}

export interface ToolCallInfo {
  name: string;
  arguments?: Record<string, unknown>;
  result?: string;
}

export interface ChatThread {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  messageCount: number;
  messages?: ThreadMessage[];
}

export interface CreateThreadRequest {
  title?: string;
}

export interface AddMessageRequest {
  role: 'User' | 'Assistant' | 'System' | 'Tool';
  content: string;
  subAgentName?: string;
  toolCalls?: ToolCallInfo[];
  metadata?: Record<string, unknown>;
}

// ============================================================================
// API Functions
// ============================================================================

/**
 * Get all chat threads ordered by last updated
 */
export async function getThreads(): Promise<ChatThread[]> {
  const response = await fetch(`${API_BASE}/threads`);
  if (!response.ok) {
    throw new Error(`Failed to get threads: ${response.statusText}`);
  }
  return response.json();
}

/**
 * Get a specific thread with all messages
 */
export async function getThread(threadId: string): Promise<ChatThread> {
  const response = await fetch(`${API_BASE}/threads/${threadId}`);
  if (!response.ok) {
    if (response.status === 404) {
      throw new Error('Thread not found');
    }
    throw new Error(`Failed to get thread: ${response.statusText}`);
  }
  return response.json();
}

/**
 * Create a new chat thread
 */
export async function createThread(title?: string): Promise<ChatThread> {
  const response = await fetch(`${API_BASE}/threads`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ title } as CreateThreadRequest),
  });
  if (!response.ok) {
    throw new Error(`Failed to create thread: ${response.statusText}`);
  }
  return response.json();
}

/**
 * Add a message to a thread (used for manual message additions)
 */
export async function addMessage(
  threadId: string,
  message: AddMessageRequest
): Promise<ThreadMessage> {
  const response = await fetch(`${API_BASE}/threads/${threadId}/messages`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(message),
  });
  if (!response.ok) {
    if (response.status === 404) {
      throw new Error('Thread not found');
    }
    throw new Error(`Failed to add message: ${response.statusText}`);
  }
  return response.json();
}

/**
 * Delete a chat thread and all its messages
 */
export async function deleteThread(threadId: string): Promise<void> {
  const response = await fetch(`${API_BASE}/threads/${threadId}`, {
    method: 'DELETE',
  });
  if (!response.ok) {
    if (response.status === 404) {
      throw new Error('Thread not found');
    }
    throw new Error(`Failed to delete thread: ${response.statusText}`);
  }
}

/**
 * Update a thread's title
 */
export async function updateThreadTitle(
  threadId: string,
  title: string
): Promise<ChatThread> {
  const response = await fetch(`${API_BASE}/threads/${threadId}`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ title }),
  });
  if (!response.ok) {
    if (response.status === 404) {
      throw new Error('Thread not found');
    }
    throw new Error(`Failed to update thread: ${response.statusText}`);
  }
  return response.json();
}
