import React from 'react';
import ReactMarkdown from 'react-markdown';
import rehypeHighlight from 'rehype-highlight';
import remarkGfm from 'remark-gfm';

interface MessageContentProps {
  content: string;
  isAgent: boolean;
}

export const MessageContent: React.FC<MessageContentProps> = ({ content, isAgent }) => {
  if (!isAgent) {
    // User messages are plain text
    return <div className="whitespace-pre-wrap">{content}</div>;
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
