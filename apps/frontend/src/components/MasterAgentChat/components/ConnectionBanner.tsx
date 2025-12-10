import React from 'react';

interface ConnectionBannerProps {
  isConnecting: boolean;
  connectionError: string | null;
  onDismissError: () => void;
}

export const ConnectionBanner: React.FC<ConnectionBannerProps> = ({
  isConnecting,
  connectionError,
  onDismissError,
}) => {
  if (isConnecting && !connectionError) {
    return (
      <div className="bg-blue-50 dark:bg-blue-900/30 border-b border-blue-200 dark:border-blue-800 p-3 animate-pulse">
        <div className="flex items-center gap-2 max-w-5xl mx-auto">
          <span className="text-blue-600 dark:text-blue-400 text-xl">🔄</span>
          <p className="text-blue-800 dark:text-blue-300 font-medium text-sm">
            Connecting to Master Agent...
          </p>
        </div>
      </div>
    );
  }

  if (connectionError) {
    return (
      <div className="bg-red-50 dark:bg-red-900/30 border-b border-red-200 dark:border-red-800 p-3 animate-slideDown">
        <div className="flex items-start gap-2 max-w-5xl mx-auto">
          <span className="text-red-600 dark:text-red-400 text-xl">⚠️</span>
          <div className="flex-1">
            <p className="text-red-800 dark:text-red-300 font-medium text-sm">Connection Error</p>
            <p className="text-red-600 dark:text-red-400 text-xs mt-1">{connectionError}</p>
            <p className="text-red-600 dark:text-red-400 text-xs mt-1">
              Make sure the backend is running:{' '}
              <code className="bg-red-100 dark:bg-red-800/40 px-1 py-0.5 rounded">
                cd src && dotnet run
              </code>
            </p>
          </div>
          <button
            onClick={onDismissError}
            className="text-red-600 dark:text-red-400 hover:text-red-800 dark:hover:text-red-300"
          >
            ✕
          </button>
        </div>
      </div>
    );
  }

  return null;
};
