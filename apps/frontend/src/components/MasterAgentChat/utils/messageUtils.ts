import { ChatMessage, TelemetryData } from '../../../services/masterAgent/types';
import { UploadedFile } from '../../FileUpload';

// Message type styling utilities
export const getMessageBubbleStyle = (type: ChatMessage['type'], toolName?: string): string => {
  if (type === 'user') {
    return 'bg-gradient-to-br from-blue-500 to-purple-600 text-white px-6 py-4 rounded-3xl shadow-lg';
  }
  if (type === 'transcription') {
    return 'bg-gradient-to-br from-purple-50 to-pink-50 dark:from-purple-900/40 dark:to-pink-900/40 border-2 border-purple-300 dark:border-purple-700 text-gray-900 dark:text-purple-100 px-6 py-4 rounded-3xl shadow-md';
  }
  if (type === 'delegation' && toolName === 'SearchWeb') {
    return 'bg-gradient-to-br from-teal-50 to-cyan-50 dark:from-teal-900/40 dark:to-cyan-900/40 border-2 border-teal-200 dark:border-teal-700 text-gray-900 dark:text-teal-100 px-6 py-4 rounded-3xl shadow-md';
  }
  if (type === 'delegation') {
    return 'bg-gradient-to-br from-purple-50 to-pink-50 dark:from-purple-900/40 dark:to-pink-900/40 border-2 border-purple-200 dark:border-purple-700 text-gray-900 dark:text-purple-100 px-6 py-4 rounded-3xl shadow-md';
  }
  if (type === 'tool') {
    return 'bg-gradient-to-br from-blue-50 to-indigo-50 dark:from-blue-900/40 dark:to-indigo-900/40 border-2 border-blue-200 dark:border-blue-700 text-gray-900 dark:text-blue-100 px-6 py-4 rounded-3xl shadow-md';
  }
  if (type === 'multimodal') {
    return 'bg-gradient-to-br from-pink-50 via-purple-50 to-indigo-50 dark:from-pink-900/40 dark:via-purple-900/40 dark:to-indigo-900/40 border-2 border-purple-300 dark:border-purple-700 text-gray-900 dark:text-purple-100 px-6 py-4 rounded-3xl shadow-lg';
  }
  if (type === 'thinking') {
    return 'bg-gradient-to-br from-yellow-50 to-amber-50 dark:from-yellow-900/40 dark:to-amber-900/40 border-2 border-yellow-200 dark:border-yellow-700 text-gray-900 dark:text-yellow-100 px-6 py-4 rounded-3xl shadow-md';
  }
  if (type === 'reasoning') {
    return 'bg-gradient-to-br from-indigo-50 to-violet-50 dark:from-indigo-900/40 dark:to-violet-900/40 border-2 border-indigo-200 dark:border-indigo-700 text-gray-900 dark:text-indigo-100 px-6 py-4 rounded-3xl shadow-md';
  }
  if (type === 'agent') {
    return 'bg-gradient-to-br from-gray-50 to-slate-50 dark:from-gray-800/40 dark:to-slate-800/40 border-2 border-gray-200 dark:border-gray-700 text-gray-900 dark:text-gray-100 px-6 py-4 rounded-3xl shadow-md';
  }
  if (type === 'telemetry') {
    return 'bg-gray-900 text-gray-100 font-mono text-xs px-6 py-4 rounded-3xl shadow-lg';
  }
  return 'bg-white dark:bg-gray-800 border-2 border-gray-200 dark:border-gray-700 text-gray-900 dark:text-gray-100 px-6 py-4 rounded-3xl shadow-md';
};

export const getMessageIcon = (type: ChatMessage['type'], toolName?: string): string | null => {
  switch (type) {
    case 'transcription':
      return '🎤';
    case 'multimodal':
      return '🎨';
    case 'thinking':
      return '🤔';
    case 'reasoning':
      return '🧠';
    case 'delegation':
      return '🔄';
    case 'tool':
      return toolName === 'SearchWeb' ? '🌐' : '🔧';
    case 'telemetry':
      return '📊';
    default:
      return null;
  }
};

export const getMessageLabel = (type: ChatMessage['type'], msg: ChatMessage): string => {
  switch (type) {
    case 'transcription':
      return `Audio Transcription${msg.metadata?.FileName ? ` - ${msg.metadata.FileName}` : ''}`;
    case 'multimodal':
      return 'Multi-Modal Response';
    case 'thinking':
      return 'Thinking';
    case 'reasoning':
      return 'Reasoning';
    case 'delegation':
      return msg.subAgentName || 'Delegation';
    case 'tool':
      return msg.toolName || 'Tool';
    default:
      return type;
  }
};

// Message utilities
export const createMessage = (message: Omit<ChatMessage, 'id' | 'timestamp'>): ChatMessage => ({
  ...message,
  id: `msg-${Date.now()}-${Math.random()}`,
  timestamp: new Date()
});

// File conversion utilities
export const convertFileToBase64 = async (file: File): Promise<string> => {
  const buffer = await file.arrayBuffer();
  const bytes = new Uint8Array(buffer);
  
  let binary = '';
  const chunkSize = 8192;
  for (let i = 0; i < bytes.length; i += chunkSize) {
    const chunk = bytes.slice(i, i + chunkSize);
    binary += String.fromCharCode(...chunk);
  }
  return btoa(binary);
};

export const prepareFileContents = async (files: UploadedFile[]) => {
  const fileDataPromises = files.map(async (uploadedFile) => {
    const base64 = await convertFileToBase64(uploadedFile.file);
    const mediaType = uploadedFile.file.type || 'application/octet-stream';
    
    let contentType: 'image' | 'audio' | 'file' = 'file';
    if (mediaType.startsWith('image/')) {
      contentType = 'image';
    } else if (mediaType.startsWith('audio/')) {
      contentType = 'audio';
    }
    
    return {
      Type: contentType,
      Data: `data:${mediaType};base64,${base64}`,
      MediaType: mediaType,
      FileName: uploadedFile.file.name
    };
  });

  return Promise.all(fileDataPromises);
};

// Telemetry utilities
export interface TelemetryContext {
  requestStart: number;
  delegationsUsed: string[];
  toolsUsed: string[];
}

export const createTelemetryContext = (): TelemetryContext => ({
  requestStart: Date.now(),
  delegationsUsed: [],
  toolsUsed: []
});

export const buildTelemetryData = (
  ctx: TelemetryContext, 
  traceId?: string
): TelemetryData => ({
  duration: Date.now() - ctx.requestStart,
  delegationCount: ctx.delegationsUsed.length,
  subAgentsUsed: [...new Set(ctx.delegationsUsed)],
  toolsUsed: [...new Set(ctx.toolsUsed)],
  traceId
});
