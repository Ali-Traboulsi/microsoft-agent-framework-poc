import React from 'react';
import { ToolCallInfo } from '../../services/masterAgent/types';

interface ToolCallCardProps {
  toolCalls: ToolCallInfo[];
}

/**
 * Compact tool call display component
 * Shows tools/delegations as a clean list with loading/completed states
 * Similar to how Copilot displays tool calls
 */
export const ToolCallCard: React.FC<ToolCallCardProps> = ({ toolCalls }) => {
  if (toolCalls.length === 0) return null;

  return (
    <div className="flex flex-col gap-1 my-2">
      {toolCalls.map((call) => (
        <div
          key={call.id}
          className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-gray-100/80 dark:bg-gray-800/60 border border-gray-200/50 dark:border-gray-700/50 text-sm"
        >
          {/* Status Icon */}
          <div className="flex-shrink-0 w-4 h-4">
            {call.status === 'running' ? (
              <svg
                className="animate-spin w-4 h-4 text-purple-500"
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
              >
                <circle
                  className="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  strokeWidth="4"
                />
                <path
                  className="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                />
              </svg>
            ) : call.status === 'completed' ? (
              <svg
                className="w-4 h-4 text-green-500"
                xmlns="http://www.w3.org/2000/svg"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z"
                  clipRule="evenodd"
                />
              </svg>
            ) : (
              <svg
                className="w-4 h-4 text-red-500"
                xmlns="http://www.w3.org/2000/svg"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z"
                  clipRule="evenodd"
                />
              </svg>
            )}
          </div>

          {/* Icon based on type */}
          <span className="text-gray-500 dark:text-gray-400">
            {call.type === 'delegation' ? '🤖' : '🔧'}
          </span>

          {/* Name */}
          <span className="text-gray-700 dark:text-gray-300 font-medium">
            {call.name}
          </span>

          {/* Error message if any */}
          {call.status === 'error' && call.error && (
            <span className="text-red-500 text-xs ml-2">
              {call.error}
            </span>
          )}
        </div>
      ))}
    </div>
  );
};

/**
 * Inline tool call indicator - ultra compact version
 * Shows just the name with spinner/checkmark
 */
export const InlineToolCall: React.FC<{
  name: string;
  status: 'running' | 'completed' | 'error';
  type: 'tool' | 'delegation';
}> = ({ name, status, type }) => {
  return (
    <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-gray-100 dark:bg-gray-800 text-xs font-medium">
      {status === 'running' ? (
        <svg
          className="animate-spin w-3 h-3 text-purple-500"
          xmlns="http://www.w3.org/2000/svg"
          fill="none"
          viewBox="0 0 24 24"
        >
          <circle
            className="opacity-25"
            cx="12"
            cy="12"
            r="10"
            stroke="currentColor"
            strokeWidth="4"
          />
          <path
            className="opacity-75"
            fill="currentColor"
            d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
          />
        </svg>
      ) : status === 'completed' ? (
        <svg
          className="w-3 h-3 text-green-500"
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 20 20"
          fill="currentColor"
        >
          <path
            fillRule="evenodd"
            d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z"
            clipRule="evenodd"
          />
        </svg>
      ) : (
        <svg
          className="w-3 h-3 text-red-500"
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 20 20"
          fill="currentColor"
        >
          <path
            fillRule="evenodd"
            d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z"
            clipRule="evenodd"
          />
        </svg>
      )}
      <span className="text-gray-600 dark:text-gray-300">
        {type === 'delegation' ? '🤖' : '🔧'} {name}
      </span>
    </span>
  );
};

export default ToolCallCard;
