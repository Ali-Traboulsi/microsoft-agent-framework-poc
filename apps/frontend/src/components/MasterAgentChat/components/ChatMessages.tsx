import React from 'react';
import type { ChatMessage } from '../../../services/masterAgent/types';
import { ProjectionResultCard } from '../../Cards/Projections/ProjectionResultCard';
import { ToolCallCard } from '../../Cards/ToolCallCard';
import { WorkflowProgressCard } from '../../Cards/WorkflowProgressCard';
import { MessageContent } from '../../MessageContent';
import { getMessageBubbleStyle, getMessageIcon, getMessageLabel } from '../utils';
import { ThinkingStepsDisplay } from './ThinkingStepsDisplay';

interface ChatMessagesProps {
  messages: ChatMessage[];
  isStreaming: boolean;
}

const StreamingIndicator: React.FC = () => (
  <div className="flex justify-start">
    <div className="bg-gray-100 dark:bg-gray-800 px-6 py-4 rounded-2xl shadow-sm">
      <div className="flex gap-2">
        <div className="w-3 h-3 bg-blue-500 rounded-full animate-bounce" />
        <div className="w-3 h-3 bg-purple-500 rounded-full animate-bounce" style={{ animationDelay: '0.1s' }} />
        <div className="w-3 h-3 bg-pink-500 rounded-full animate-bounce" style={{ animationDelay: '0.2s' }} />
      </div>
    </div>
  </div>
);

const MessageBubble: React.FC<{ msg: ChatMessage }> = ({ msg }) => {
  // Special rendering for thinking messages with steps - minimalistic outside bubble
  if ((msg.type === 'thinking' || msg.type === 'reasoning') && msg.thinkingSteps && msg.thinkingSteps.length > 0) {
    return (
      <div className="w-full max-w-4xl">
        <ThinkingStepsDisplay steps={msg.thinkingSteps} />
      </div>
    );
  }

  // Hide thinking messages that only have content (redundant with delegation events)
  if (msg.type === 'thinking' && !msg.thinkingSteps) {
    return null;
  }

  // Special rendering for projection results
  if (msg.type === 'projection' && msg.projectionResult) {
    return (
      <div className="w-full max-w-5xl mx-auto">
        <ProjectionResultCard result={msg.projectionResult} />
      </div>
    );
  }

  // Workflow progress card
  if (msg.type === 'workflow-progress' && msg.workflowSteps) {
    return (
      <div className="w-full max-w-3xl mx-auto">
        <WorkflowProgressCard steps={msg.workflowSteps} />
      </div>
    );
  }

  // Tool calls card
  if (msg.type === 'tool-calls' && msg.toolCalls) {
    return (
      <div className="w-full max-w-3xl">
        <ToolCallCard toolCalls={msg.toolCalls} />
      </div>
    );
  }

  const icon = getMessageIcon(msg.type, msg.toolName);

  // Standard message bubble
  return (
    <div
      className={`max-w-4xl ${msg.type === 'user' ? 'ml-auto' : 'mr-auto'} ${getMessageBubbleStyle(
        msg.type,
        msg.toolName
      )}`}
    >
      {msg.type !== 'user' && icon && (
        <div className="flex items-center gap-2 mb-2">
          <span className="text-xl">{icon}</span>
          <span className="text-xs font-semibold uppercase tracking-wide opacity-70">
            {getMessageLabel(msg.type, msg)}
          </span>
        </div>
      )}

      {/* Attached files for user messages */}
      {msg.files && msg.files.length > 0 && (
        <div className="mb-3 grid grid-cols-2 gap-2">
          {msg.files.map((file) => (
            <div
              key={file.id}
              className="flex items-center gap-2 bg-white/80 backdrop-blur rounded-lg p-2 text-xs border border-white/20"
            >
              {file.preview ? (
                <img src={file.preview} alt={file.file.name} className="w-12 h-12 object-cover rounded" />
              ) : (
                <span className="text-2xl">{file.file.type.startsWith('audio/') ? '🎵' : '📄'}</span>
              )}
              <div className="flex-1 truncate">
                <div className="font-medium">{file.file.name}</div>
                <div className="text-gray-600">{(file.file.size / 1024).toFixed(1)} KB</div>
              </div>
            </div>
          ))}
        </div>
      )}

      {msg.type === 'telemetry' ? (
        <pre className="text-xs bg-gray-900 text-gray-100 p-3 rounded-lg overflow-x-auto">{msg.content}</pre>
      ) : (
        <MessageContent content={msg.content} isAgent={msg.type !== 'user'} />
      )}

      <div className="flex items-center justify-between mt-2 text-xs opacity-60">
        <span>{msg.timestamp.toLocaleTimeString()}</span>
        {msg.metadata && 'contentTypes' in msg.metadata && Array.isArray(msg.metadata.contentTypes) && (
          <div className="flex gap-1">
            {(msg.metadata.contentTypes as string[]).map((type: string) => (
              <span key={type} className="px-2 py-0.5 bg-white/20 rounded-full">
                {type}
              </span>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};

export const ChatMessages: React.FC<ChatMessagesProps> = ({ messages, isStreaming }) => {
  return (
    <>
      {messages.map((msg) => (
        <div key={msg.id} className={`flex ${msg.type === 'user' ? 'justify-end' : 'justify-start'}`}>
          <MessageBubble msg={msg} />
        </div>
      ))}

      {isStreaming && <StreamingIndicator />}
    </>
  );
};
