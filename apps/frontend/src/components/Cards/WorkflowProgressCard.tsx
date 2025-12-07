import React from 'react';

export interface WorkflowStep {
  stepId: string;
  stepName: string;
  stepNameAr: string;
  stepNumber: number;
  totalSteps: number;
  isCompleted: boolean;
  durationMs?: number;
  details?: string;
}

interface WorkflowProgressCardProps {
  steps: WorkflowStep[];
  showArabic?: boolean;
}

export const WorkflowProgressCard: React.FC<WorkflowProgressCardProps> = ({ 
  steps, 
  showArabic = false 
}) => {
  if (!steps || steps.length === 0) {
    return null;
  }

  // Get total steps from the first step's metadata
  const totalSteps = steps[0]?.totalSteps || 7;
  const completedSteps = steps.filter(s => s.isCompleted).length;
  const currentStep = steps.find(s => !s.isCompleted);
  const progress = (completedSteps / totalSteps) * 100;

  return (
    <div className="bg-gradient-to-br from-indigo-50 to-purple-50 dark:from-indigo-900/30 dark:to-purple-900/30 
                    border border-indigo-200 dark:border-indigo-700 rounded-2xl p-5 shadow-lg">
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <span className="text-2xl">📊</span>
          <h3 className="font-semibold text-indigo-900 dark:text-indigo-100">
            {showArabic ? 'تقدم التحليل' : 'Analysis Progress'}
          </h3>
        </div>
        <span className="text-sm font-medium text-indigo-600 dark:text-indigo-400">
          {completedSteps}/{totalSteps} {showArabic ? 'خطوات' : 'steps'}
        </span>
      </div>

      {/* Progress Bar */}
      <div className="relative h-2 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden mb-4">
        <div 
          className="absolute inset-y-0 left-0 bg-gradient-to-r from-indigo-500 to-purple-500 rounded-full transition-all duration-500 ease-out"
          style={{ width: `${progress}%` }}
        />
        {/* Animated pulse on the progress edge */}
        {progress > 0 && progress < 100 && (
          <div 
            className="absolute inset-y-0 w-2 bg-white/50 rounded-full animate-pulse"
            style={{ left: `calc(${progress}% - 4px)` }}
          />
        )}
      </div>

      {/* Steps List */}
      <div className="space-y-2">
        {steps.map((step) => (
          <div 
            key={step.stepId}
            className={`flex items-center gap-3 p-2 rounded-lg transition-all duration-300 ${
              step.isCompleted 
                ? 'bg-green-100/50 dark:bg-green-900/20' 
                : currentStep?.stepId === step.stepId
                  ? 'bg-indigo-100/50 dark:bg-indigo-900/20 animate-pulse'
                  : 'bg-gray-100/50 dark:bg-gray-800/30'
            }`}
          >
            {/* Step Indicator */}
            <div className={`w-7 h-7 rounded-full flex items-center justify-center text-sm font-medium transition-all duration-300 ${
              step.isCompleted
                ? 'bg-green-500 text-white'
                : currentStep?.stepId === step.stepId
                  ? 'bg-indigo-500 text-white animate-bounce'
                  : 'bg-gray-300 dark:bg-gray-600 text-gray-600 dark:text-gray-300'
            }`}>
              {step.isCompleted ? '✓' : step.stepNumber}
            </div>

            {/* Step Content */}
            <div className="flex-1 min-w-0">
              <div className="flex items-center justify-between">
                <span className={`text-sm font-medium truncate ${
                  step.isCompleted 
                    ? 'text-green-700 dark:text-green-300' 
                    : currentStep?.stepId === step.stepId
                      ? 'text-indigo-700 dark:text-indigo-300'
                      : 'text-gray-600 dark:text-gray-400'
                }`}>
                  {showArabic ? step.stepNameAr : step.stepName}
                </span>
                {step.durationMs && (
                  <span className="text-xs text-gray-500 dark:text-gray-400 ml-2">
                    {step.durationMs}ms
                  </span>
                )}
              </div>
              {step.details && step.isCompleted && (
                <span className="text-xs text-gray-500 dark:text-gray-400 truncate block">
                  {step.details}
                </span>
              )}
            </div>

            {/* Status Icon */}
            <div className="flex-shrink-0">
              {step.isCompleted ? (
                <span className="text-green-500">✅</span>
              ) : currentStep?.stepId === step.stepId ? (
                <span className="text-indigo-500 animate-spin">⏳</span>
              ) : (
                <span className="text-gray-400">○</span>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* Current Status */}
      {currentStep && (
        <div className="mt-4 pt-3 border-t border-indigo-200/50 dark:border-indigo-700/50">
          <div className="flex items-center gap-2 text-sm text-indigo-600 dark:text-indigo-400">
            <span className="animate-pulse">🔄</span>
            <span>{showArabic ? currentStep.stepNameAr : currentStep.stepName}...</span>
          </div>
        </div>
      )}

      {/* Completion Message */}
      {completedSteps === totalSteps && (
        <div className="mt-4 pt-3 border-t border-green-200/50 dark:border-green-700/50">
          <div className="flex items-center gap-2 text-sm text-green-600 dark:text-green-400 font-medium">
            <span>✨</span>
            <span>{showArabic ? 'اكتمل التحليل!' : 'Analysis Complete!'}</span>
          </div>
        </div>
      )}
    </div>
  );
};
