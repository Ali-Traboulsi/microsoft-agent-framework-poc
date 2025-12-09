import React, { useState } from 'react';
import ReactMarkdown from 'react-markdown';
import rehypeHighlight from 'rehype-highlight';
import remarkGfm from 'remark-gfm';
import { FundInWorkflowCard, parseFundInWorkflowContent } from './Cards/FundInWorkflowCard';

interface MessageContentProps {
  content: string;
  isAgent: boolean;
}

// Try to detect and parse structured content
function tryParseStructured(content: string): any | null {
  try {
    // Check if content contains JSON blocks
    const jsonMatch = content.match(/```json\s*\n([\s\S]*?)\n```/);
    if (jsonMatch) {
      return JSON.parse(jsonMatch[1]);
    }

    // Check if entire content is JSON
    if (content.trim().startsWith('{') || content.trim().startsWith('[')) {
      return JSON.parse(content);
    }

    // Check for audio transcription format
    if (content.includes('[Audio transcription from')) {
      const match = content.match(/\[Audio transcription from (.+?)\]: (.+?)(?:\n\n(.+))?$/s);
      if (match) {
        return {
          type: 'audio-transcription',
          filename: match[1],
          transcript: match[2],
          analysis: match[3]
        };
      }
    }

    return null;
  } catch {
    return null;
  }
}

// Render structured data as cards
const StructuredDataCard: React.FC<{ data: any }> = ({ data }) => {
  const [expanded, setExpanded] = useState(true);

  // Handle audio transcription
  if (data.type === 'audio-transcription') {
    return (
      <div className="space-y-3">
        <div className="bg-purple-50 border border-purple-200 rounded-lg p-4">
          <div className="flex items-center gap-2 mb-3">
            <span className="text-2xl">🎵</span>
            <div className="flex-1">
              <div className="font-semibold text-purple-900">Audio Transcription</div>
              <div className="text-sm text-purple-700">{data.filename}</div>
            </div>
          </div>
          <div className="bg-white rounded p-3 text-sm text-gray-700 italic border border-purple-100">
            "{data.transcript}"
          </div>
        </div>
        {data.analysis && (
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
            <div className="font-semibold text-blue-900 mb-2">AI Analysis</div>
            <div className="text-sm text-gray-700">
              <ReactMarkdown remarkPlugins={[remarkGfm]}>
                {data.analysis}
              </ReactMarkdown>
            </div>
          </div>
        )}
      </div>
    );
  }

  // Handle arrays
  if (Array.isArray(data)) {
    return (
      <div className="space-y-2">
        <div className="flex items-center justify-between mb-2">
          <span className="text-sm font-medium text-gray-700">
            Array ({data.length} items)
          </span>
          <button
            onClick={() => setExpanded(!expanded)}
            className="text-xs text-blue-600 hover:text-blue-800"
          >
            {expanded ? 'Collapse' : 'Expand'}
          </button>
        </div>
        {expanded && (
          <div className="space-y-2">
            {data.map((item, idx) => (
              <div key={idx} className="bg-gray-50 rounded-lg p-3 border border-gray-200">
                <StructuredDataCard data={item} />
              </div>
            ))}
          </div>
        )}
      </div>
    );
  }

  // Handle objects
  if (typeof data === 'object' && data !== null) {
    const keys = Object.keys(data);
    
    return (
      <div className="bg-gradient-to-br from-gray-50 to-gray-100 rounded-lg p-4 border border-gray-200 shadow-sm">
        <div className="space-y-2">
          {keys.map((key) => {
            const value = data[key];
            const isNested = typeof value === 'object' && value !== null;
            
            return (
              <div key={key} className="flex gap-3">
                <div className="flex-shrink-0 font-semibold text-gray-700 min-w-[120px]">
                  {key}:
                </div>
                <div className="flex-1">
                  {isNested ? (
                    <StructuredDataCard data={value} />
                  ) : (
                    <span className="text-gray-900">
                      {typeof value === 'boolean' ? (
                        value ? '✅ True' : '❌ False'
                      ) : (
                        String(value)
                      )}
                    </span>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    );
  }

  // Fallback for primitives
  return <span className="text-gray-900">{String(data)}</span>;
};

export const MessageContent: React.FC<MessageContentProps> = ({ content, isAgent }) => {
  if (!isAgent) {
    // User messages are plain text
    return <div className="whitespace-pre-wrap">{content}</div>;
  }

  // Try to detect Fund-In workflow content first
  const fundInContent = parseFundInWorkflowContent(content);
  if (fundInContent && fundInContent.steps.length > 0) {
    return (
      <FundInWorkflowCard
        steps={fundInContent.steps}
        result={fundInContent.result}
        isAwaitingOtp={fundInContent.isAwaitingOtp}
      />
    );
  }

  // Try to detect structured content
  const structured = tryParseStructured(content);
  
  if (structured) {
    return (
      <div className="space-y-3">
        <StructuredDataCard data={structured} />
      </div>
    );
  }

  // Agent messages support markdown
  return (
    <div className="markdown-content">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeHighlight]}
        components={{
          // Custom rendering for code blocks
          code({ className, children, ...props }: any) {
            const isInline = !props.node?.position || props.node.position.start.line === props.node.position.end.line;
            return !isInline ? (
              <code
                className={`${className || ''} block bg-gray-800 text-gray-100 p-3 rounded-md overflow-x-auto`}
                {...props}
              >
                {children}
              </code>
            ) : (
              <code className="bg-gray-700 text-gray-100 px-1.5 py-0.5 rounded text-sm" {...props}>
                {children}
              </code>
            );
          },
        // Custom rendering for links
        a({ node, children, ...props }) {
          return (
            <a
              className="text-blue-400 hover:text-blue-300 underline"
              target="_blank"
              rel="noopener noreferrer"
              {...props}
            >
              {children}
            </a>
          );
        },
        // Custom rendering for paragraphs
        p({ node, children, ...props }) {
          return (
            <p className="mb-2 last:mb-0" {...props}>
              {children}
            </p>
          );
        },
        // Custom rendering for headings
        h1({ node, children, ...props }) {
          return (
            <h1 className="text-xl font-bold mt-4 mb-2 first:mt-0" {...props}>
              {children}
            </h1>
          );
        },
        h2({ node, children, ...props }) {
          return (
            <h2 className="text-lg font-bold mt-3 mb-2 first:mt-0" {...props}>
              {children}
            </h2>
          );
        },
        h3({ node, children, ...props }) {
          return (
            <h3 className="text-base font-bold mt-2 mb-1 first:mt-0" {...props}>
              {children}
            </h3>
          );
        },
        // Custom rendering for lists
        ul({ node, children, ...props }) {
          return (
            <ul className="list-disc list-inside mb-2 space-y-1" {...props}>
              {children}
            </ul>
          );
        },
        ol({ node, children, ...props }) {
          return (
            <ol className="list-decimal list-inside mb-2 space-y-1" {...props}>
              {children}
            </ol>
          );
        },
        // Custom rendering for list items
        li({ node, children, ...props }) {
          return (
            <li className="ml-2" {...props}>
              {children}
            </li>
          );
        },
        // Custom rendering for blockquotes
        blockquote({ node, children, ...props }) {
          return (
            <blockquote
              className="border-l-4 border-gray-600 pl-4 italic my-2"
              {...props}
            >
              {children}
            </blockquote>
          );
        },
        // Custom rendering for tables
        table({ node, children, ...props }) {
          return (
            <div className="overflow-x-auto my-2">
              <table className="min-w-full border border-gray-600" {...props}>
                {children}
              </table>
            </div>
          );
        },
        th({ node, children, ...props }) {
          return (
            <th
              className="border border-gray-600 px-3 py-2 bg-gray-700 font-semibold text-left"
              {...props}
            >
              {children}
            </th>
          );
        },
        td({ node, children, ...props }) {
          return (
            <td className="border border-gray-600 px-3 py-2" {...props}>
              {children}
            </td>
          );
        },
        // Custom rendering for horizontal rules
        hr({ node, ...props }) {
          return <hr className="my-4 border-gray-600" {...props} />;
        },
      }}
    >
      {content}
    </ReactMarkdown>
    </div>
  );
};
