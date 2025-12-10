import React from 'react';
import { FileUpload, type UploadedFile } from '../../FileUpload';

interface ChatInputProps {
  input: string;
  onInputChange: (input: string) => void;
  onSend: () => void;
  isStreaming: boolean;
  enableThinking: boolean;
  onToggleThinking: () => void;
  showFileUpload: boolean;
  onToggleFileUpload: () => void;
  uploadedFiles: UploadedFile[];
  onFilesChange: (files: UploadedFile[]) => void;
}

export const ChatInput: React.FC<ChatInputProps> = ({
  input,
  onInputChange,
  onSend,
  isStreaming,
  enableThinking,
  onToggleThinking,
  showFileUpload,
  onToggleFileUpload,
  uploadedFiles,
  onFilesChange,
}) => {
  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      onSend();
    }
  };

  return (
    <div className="border-t dark:border-gray-700 bg-white dark:bg-gray-900 shadow-2xl">
      {/* File Upload Panel */}
      {showFileUpload && (
        <div className="border-b dark:border-gray-700 bg-gray-50 dark:bg-gray-800 px-6 py-4 animate-slideDown max-w-5xl mx-auto">
          <div className="flex items-center justify-between mb-3">
            <h3 className="font-semibold text-gray-900 dark:text-gray-100 flex items-center gap-2">
              <span className="text-xl">📎</span>
              Attach Files
            </h3>
            <button
              onClick={() => {
                onToggleFileUpload();
                onFilesChange([]);
              }}
              className="text-gray-400 dark:text-gray-500 hover:text-gray-600 dark:hover:text-gray-300 transition-colors"
            >
              ✕
            </button>
          </div>
          <FileUpload files={uploadedFiles} onFilesChange={onFilesChange} maxFiles={5} />
        </div>
      )}

      <div className="max-w-5xl mx-auto px-6 py-4">
        {/* File Chips */}
        {uploadedFiles.length > 0 && !showFileUpload && (
          <div className="mb-3 flex flex-wrap gap-2">
            {uploadedFiles.map((file) => (
              <div
                key={file.id}
                className="flex items-center gap-2 bg-gradient-to-r from-blue-50 to-purple-50 dark:from-blue-900/40 dark:to-purple-900/40 border border-blue-200 dark:border-blue-700 rounded-full px-4 py-2 text-sm group hover:shadow-md transition-all"
              >
                <span className="text-lg">
                  {file.file.type.startsWith('image/')
                    ? '🖼️'
                    : file.file.type.startsWith('audio/')
                    ? '🎵'
                    : '📄'}
                </span>
                <span className="max-w-[200px] truncate font-medium text-gray-900 dark:text-gray-100">
                  {file.file.name}
                </span>
                <button
                  onClick={() => onFilesChange(uploadedFiles.filter((f) => f.id !== file.id))}
                  className="text-blue-600 dark:text-blue-400 hover:text-red-600 dark:hover:text-red-400 transition-colors font-bold"
                >
                  ✕
                </button>
              </div>
            ))}
          </div>
        )}

        <div className="flex gap-3">
          {/* Thinking Mode Toggle */}
          <button
            onClick={onToggleThinking}
            disabled={isStreaming}
            className={`
              flex-shrink-0 w-14 h-14 flex items-center justify-center rounded-xl
              transition-all duration-200 disabled:opacity-50 shadow-md hover:shadow-lg
              ${
                enableThinking
                  ? 'bg-gradient-to-br from-purple-500 to-purple-600 text-white scale-105'
                  : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 border-2 border-gray-200 dark:border-gray-600'
              }
            `}
            title={
              enableThinking
                ? 'Thinking mode enabled - Extended reasoning active'
                : 'Enable thinking mode - Show detailed reasoning process'
            }
          >
            <span className="text-2xl">🤔</span>
          </button>

          {/* Attach Button */}
          <button
            onClick={onToggleFileUpload}
            disabled={isStreaming}
            className={`
              flex-shrink-0 w-14 h-14 flex items-center justify-center rounded-xl
              transition-all duration-200 disabled:opacity-50 shadow-md hover:shadow-lg
              ${
                showFileUpload
                  ? 'bg-gradient-to-br from-blue-500 to-blue-600 text-white scale-105'
                  : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 border-2 border-gray-200 dark:border-gray-600'
              }
            `}
            title="Attach files (images, audio, documents)"
          >
            <span className="text-2xl">{showFileUpload ? '✕' : '📎'}</span>
          </button>

          {/* Text Input */}
          <input
            type="text"
            value={input}
            onChange={(e) => onInputChange(e.target.value)}
            onKeyPress={handleKeyPress}
            placeholder={uploadedFiles.length > 0 ? 'Add a message (optional)...' : 'Type your message...'}
            disabled={isStreaming}
            className="flex-1 px-6 py-4 text-lg border-2 border-gray-200 dark:border-gray-600 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50 transition-all shadow-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 placeholder-gray-400 dark:placeholder-gray-500"
          />

          {/* Send Button */}
          <button
            onClick={onSend}
            disabled={isStreaming || (!input.trim() && uploadedFiles.length === 0)}
            className="flex-shrink-0 px-8 py-4 bg-gradient-to-r from-blue-500 via-purple-500 to-pink-500 text-white rounded-xl hover:from-blue-600 hover:via-purple-600 hover:to-pink-600 disabled:opacity-50 disabled:cursor-not-allowed transition-all font-bold shadow-lg hover:shadow-xl transform hover:scale-105 active:scale-95 text-lg"
          >
            {isStreaming ? (
              <span className="flex items-center gap-2">
                <span className="animate-spin">⏳</span>
                Processing
              </span>
            ) : (
              <span className="flex items-center gap-2">
                {uploadedFiles.length > 0 ? '📤' : '🚀'}
                Send
              </span>
            )}
          </button>
        </div>

        {uploadedFiles.length > 0 && (
          <p className="text-xs text-blue-600 dark:text-blue-400 font-medium mt-2 text-center">
            {uploadedFiles.length} file{uploadedFiles.length > 1 ? 's' : ''} attached · Ready to send
          </p>
        )}
      </div>
    </div>
  );
};
