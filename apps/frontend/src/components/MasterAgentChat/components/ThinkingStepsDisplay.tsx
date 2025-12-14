import React from 'react';
import type { ThinkingStep } from '../../../services/masterAgent/types';

interface ThinkingStepsDisplayProps {
  steps: ThinkingStep[];
}

const StepLine: React.FC<{ step: ThinkingStep; isLast: boolean }> = ({ step, isLast }) => {
  const isRunning = step.durationMs === undefined;

  return (
    <div className={`flex items-center gap-2 py-1 ${isLast && isRunning ? 'animate-pulse' : ''}`}>
      <span className="text-base">{step.emoji}</span>
      <span className="text-sm text-gray-600 dark:text-gray-400">
        {step.label}
        {step.details && ` ${step.details}`}
      </span>
      {isRunning && isLast && (
        <span className="inline-block w-1.5 h-1.5 bg-blue-500 rounded-full animate-ping" />
      )}
    </div>
  );
};

export const ThinkingStepsDisplay: React.FC<ThinkingStepsDisplayProps> = ({ steps }) => {
  if (!steps || steps.length === 0) return null;

  return (
    <div className="py-2 px-1 space-y-0.5 border-l-2 border-gray-200 dark:border-gray-700 pl-3 ml-1">
      {steps.map((step, index) => (
        <StepLine key={step.id} step={step} isLast={index === steps.length - 1} />
      ))}
    </div>
  );
};
