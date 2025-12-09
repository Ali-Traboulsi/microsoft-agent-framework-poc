import React, { useMemo } from 'react';

export interface FundInStep {
  icon: string;
  step: string;
  message: string;
  status: 'starting' | 'in-progress' | 'completed' | 'failed';
}

export interface FundInResult {
  success: boolean;
  referenceNumber?: string;
  transactionId?: string;
  amount?: string;
  fees?: string;
  totalAmount?: string;
  units?: string;
  navAtPurchase?: string;
  fundName?: string;
  sourceAccount?: string;
  targetPortfolio?: string;
  duration?: string;
  summaryEn?: string;
  summaryAr?: string;
  error?: {
    failedStep?: string;
    message?: string;
    errorCode?: string;
  };
  workflowId?: string;
  otpSentTo?: string;
  nextStep?: string;
}

interface FundInWorkflowCardProps {
  steps: FundInStep[];
  result?: FundInResult;
  isAwaitingOtp?: boolean;
}

// Parse workflow progress from markdown content
export function parseFundInWorkflowContent(content: string): {
  steps: FundInStep[];
  result?: FundInResult;
  isAwaitingOtp: boolean;
} | null {
  // Check if this is a Fund-In workflow response
  if (!content.includes('Fund-In Workflow Progress') && 
      !content.includes('Fund-In Workflow Completion')) {
    return null;
  }

  const steps: FundInStep[] = [];
  let result: FundInResult | undefined;
  let isAwaitingOtp = false;

  // Parse step lines (format: 🔄 **Step**: Message or ✅ **Step**: Message)
  const stepRegex = /^([🔄⏳✅❌•])\s*\*\*(.+?)\*\*:\s*(.+)$/gm;
  let match;

  while ((match = stepRegex.exec(content)) !== null) {
    const [, icon, step, message] = match;
    let status: FundInStep['status'] = 'in-progress';
    
    if (icon === '✅') status = 'completed';
    else if (icon === '❌') status = 'failed';
    else if (icon === '🔄') status = 'starting';
    else if (icon === '⏳') status = 'in-progress';

    steps.push({ icon, step, message, status });
  }

  // Check for awaiting OTP
  if (content.includes('Ready for OTP Verification') || 
      content.includes('Waiting for OTP verification')) {
    isAwaitingOtp = true;
  }

  // Parse result table
  const tableMatch = content.match(/\| Field \| Value \|([\s\S]*?)(?=\n\n|\*\*|$)/);
  if (tableMatch) {
    const tableContent = tableMatch[1];
    const rows = tableContent.split('\n').filter(line => line.includes('|') && !line.includes('---'));
    
    result = {
      success: content.includes('Transaction Completed Successfully'),
    };

    for (const row of rows) {
      const [, field, value] = row.split('|').map(s => s?.trim());
      if (!field || !value) continue;

      switch (field) {
        case 'Reference Number':
          result.referenceNumber = value;
          break;
        case 'Transaction ID':
          result.transactionId = value;
          break;
        case 'Workflow ID':
          result.workflowId = value;
          break;
        case 'Amount':
          result.amount = value;
          break;
        case 'Fees':
          result.fees = value;
          break;
        case 'Total Amount':
          result.totalAmount = value;
          break;
        case 'Units Purchased':
          result.units = value;
          break;
        case 'NAV at Purchase':
          result.navAtPurchase = value;
          break;
        case 'Fund Name':
          result.fundName = value;
          break;
        case 'Source Account':
          result.sourceAccount = value;
          break;
        case 'Target Portfolio':
          result.targetPortfolio = value;
          break;
        case 'Duration':
          result.duration = value;
          break;
        case 'Failed Step':
          if (!result.error) result.error = {};
          result.error.failedStep = value;
          break;
        case 'Error':
          if (!result.error) result.error = {};
          result.error.message = value;
          break;
        case 'Error Code':
          if (!result.error) result.error = {};
          result.error.errorCode = value;
          break;
        case 'OTP Sent To':
          result.otpSentTo = value;
          break;
      }
    }

    // Parse summaries
    const enSummaryMatch = content.match(/\*\*English Summary\*\*:\s*(.+)/);
    const arSummaryMatch = content.match(/\*\*Arabic Summary\*\*:\s*(.+)/);
    const nextStepMatch = content.match(/\*\*Next Step\*\*:\s*(.+)/);
    
    if (enSummaryMatch) result.summaryEn = enSummaryMatch[1];
    if (arSummaryMatch) result.summaryAr = arSummaryMatch[1];
    if (nextStepMatch) result.nextStep = nextStepMatch[1];
  }

  // Check for failure
  if (content.includes('Transaction Failed') || content.includes('Workflow Failed')) {
    if (result) {
      result.success = false;
    } else {
      result = { success: false };
    }
  }

  return { steps, result, isAwaitingOtp };
}

export const FundInWorkflowCard: React.FC<FundInWorkflowCardProps> = ({
  steps,
  result,
  isAwaitingOtp = false,
}) => {
  const completedCount = useMemo(() => 
    steps.filter(s => s.status === 'completed').length, 
    [steps]
  );

  const currentStep = useMemo(() => 
    steps.find(s => s.status === 'in-progress' || s.status === 'starting'),
    [steps]
  );

  const progress = steps.length > 0 ? (completedCount / steps.length) * 100 : 0;

  return (
    <div className="bg-gradient-to-br from-emerald-50 to-teal-50 dark:from-emerald-900/30 dark:to-teal-900/30 
                    border border-emerald-200 dark:border-emerald-700 rounded-2xl p-5 shadow-lg space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <span className="text-3xl">💰</span>
          <div>
            <h3 className="font-bold text-emerald-900 dark:text-emerald-100 text-lg">
              Fund-In Workflow
            </h3>
            <p className="text-sm text-emerald-600 dark:text-emerald-400">
              تحويل الأموال للاستثمار
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-emerald-700 dark:text-emerald-300">
            {completedCount}/{steps.length}
          </span>
          {result?.success && <span className="text-2xl">✅</span>}
          {result?.success === false && <span className="text-2xl">❌</span>}
          {isAwaitingOtp && <span className="text-2xl animate-pulse">📱</span>}
        </div>
      </div>

      {/* Progress Bar */}
      <div className="relative h-3 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
        <div
          className={`absolute inset-y-0 left-0 rounded-full transition-all duration-700 ease-out ${
            result?.success === false
              ? 'bg-gradient-to-r from-red-400 to-red-500'
              : result?.success
                ? 'bg-gradient-to-r from-emerald-400 to-teal-500'
                : 'bg-gradient-to-r from-emerald-400 to-teal-500'
          }`}
          style={{ width: `${progress}%` }}
        />
        {progress > 0 && progress < 100 && !result && (
          <div
            className="absolute inset-y-0 w-3 bg-white/60 rounded-full animate-pulse"
            style={{ left: `calc(${progress}% - 6px)` }}
          />
        )}
      </div>

      {/* Steps Timeline */}
      <div className="space-y-2">
        {steps.map((step, index) => (
          <div
            key={`${step.step}-${index}`}
            className={`flex items-start gap-3 p-3 rounded-xl transition-all duration-300 ${
              step.status === 'completed'
                ? 'bg-emerald-100/60 dark:bg-emerald-800/30'
                : step.status === 'failed'
                  ? 'bg-red-100/60 dark:bg-red-800/30'
                  : step.status === 'in-progress' || step.status === 'starting'
                    ? 'bg-teal-100/60 dark:bg-teal-800/30'
                    : 'bg-gray-100/40 dark:bg-gray-800/20'
            }`}
          >
            {/* Step Icon */}
            <div className={`w-10 h-10 rounded-full flex items-center justify-center text-lg flex-shrink-0 ${
              step.status === 'completed'
                ? 'bg-emerald-500 text-white'
                : step.status === 'failed'
                  ? 'bg-red-500 text-white'
                  : step.status === 'in-progress' || step.status === 'starting'
                    ? 'bg-teal-500 text-white animate-pulse'
                    : 'bg-gray-300 dark:bg-gray-600'
            }`}>
              {step.status === 'completed' ? '✓' : 
               step.status === 'failed' ? '✗' :
               step.status === 'in-progress' ? '⏳' :
               step.status === 'starting' ? '🔄' : (index + 1)}
            </div>

            {/* Step Content */}
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2">
                <span className={`font-semibold text-sm ${
                  step.status === 'completed'
                    ? 'text-emerald-800 dark:text-emerald-200'
                    : step.status === 'failed'
                      ? 'text-red-800 dark:text-red-200'
                      : 'text-teal-800 dark:text-teal-200'
                }`}>
                  {step.step}
                </span>
              </div>
              <p className={`text-sm mt-0.5 ${
                step.status === 'failed'
                  ? 'text-red-600 dark:text-red-300'
                  : 'text-gray-600 dark:text-gray-300'
              }`}>
                {step.message.replace(/^[🚀📋🔍📝🔐🔒✅❌⏳🔄🎉]\s*/, '')}
              </p>
            </div>

            {/* Status Badge */}
            <div className="flex-shrink-0">
              {step.status === 'completed' && (
                <span className="px-2 py-1 bg-emerald-200 dark:bg-emerald-700 text-emerald-800 dark:text-emerald-100 text-xs rounded-full">
                  Done
                </span>
              )}
              {step.status === 'failed' && (
                <span className="px-2 py-1 bg-red-200 dark:bg-red-700 text-red-800 dark:text-red-100 text-xs rounded-full">
                  Failed
                </span>
              )}
              {(step.status === 'in-progress' || step.status === 'starting') && (
                <span className="px-2 py-1 bg-teal-200 dark:bg-teal-700 text-teal-800 dark:text-teal-100 text-xs rounded-full animate-pulse">
                  Running
                </span>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* Awaiting OTP Message */}
      {isAwaitingOtp && result && (
        <div className="bg-amber-100 dark:bg-amber-900/40 border border-amber-300 dark:border-amber-600 rounded-xl p-4">
          <div className="flex items-center gap-3 mb-3">
            <span className="text-2xl animate-bounce">📱</span>
            <div>
              <h4 className="font-semibold text-amber-800 dark:text-amber-200">
                OTP Verification Required
              </h4>
              <p className="text-sm text-amber-600 dark:text-amber-300">
                مطلوب رمز التحقق
              </p>
            </div>
          </div>
          <div className="bg-white/60 dark:bg-black/20 rounded-lg p-3 space-y-2 text-sm">
            {result.workflowId && (
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Workflow ID:</span>
                <code className="bg-gray-200 dark:bg-gray-700 px-2 py-0.5 rounded text-xs">
                  {result.workflowId}
                </code>
              </div>
            )}
            {result.transactionId && (
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Transaction ID:</span>
                <span className="font-mono">{result.transactionId}</span>
              </div>
            )}
            {result.otpSentTo && (
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">OTP Sent To:</span>
                <span>{result.otpSentTo}</span>
              </div>
            )}
          </div>
          {result.nextStep && (
            <p className="mt-3 text-sm text-amber-700 dark:text-amber-300 italic">
              👉 {result.nextStep}
            </p>
          )}
        </div>
      )}

      {/* Success Result */}
      {result?.success && (
        <div className="bg-emerald-100 dark:bg-emerald-900/40 border border-emerald-300 dark:border-emerald-600 rounded-xl p-4">
          <div className="flex items-center gap-3 mb-3">
            <span className="text-2xl">🎉</span>
            <div>
              <h4 className="font-semibold text-emerald-800 dark:text-emerald-200">
                Transaction Successful!
              </h4>
              <p className="text-sm text-emerald-600 dark:text-emerald-300">
                تمت المعاملة بنجاح!
              </p>
            </div>
          </div>

          {/* Transaction Details Grid */}
          <div className="grid grid-cols-2 gap-3 text-sm">
            {result.referenceNumber && (
              <div className="bg-white/60 dark:bg-black/20 rounded-lg p-2">
                <div className="text-gray-500 dark:text-gray-400 text-xs">Reference</div>
                <div className="font-semibold text-emerald-800 dark:text-emerald-200">
                  {result.referenceNumber}
                </div>
              </div>
            )}
            {result.amount && (
              <div className="bg-white/60 dark:bg-black/20 rounded-lg p-2">
                <div className="text-gray-500 dark:text-gray-400 text-xs">Amount</div>
                <div className="font-semibold text-emerald-800 dark:text-emerald-200">
                  {result.amount}
                </div>
              </div>
            )}
            {result.fees && (
              <div className="bg-white/60 dark:bg-black/20 rounded-lg p-2">
                <div className="text-gray-500 dark:text-gray-400 text-xs">Fees</div>
                <div className="font-semibold">{result.fees}</div>
              </div>
            )}
            {result.totalAmount && (
              <div className="bg-white/60 dark:bg-black/20 rounded-lg p-2">
                <div className="text-gray-500 dark:text-gray-400 text-xs">Total</div>
                <div className="font-semibold text-emerald-800 dark:text-emerald-200">
                  {result.totalAmount}
                </div>
              </div>
            )}
            {result.units && (
              <div className="bg-white/60 dark:bg-black/20 rounded-lg p-2">
                <div className="text-gray-500 dark:text-gray-400 text-xs">Units</div>
                <div className="font-semibold">{result.units}</div>
              </div>
            )}
            {result.duration && (
              <div className="bg-white/60 dark:bg-black/20 rounded-lg p-2">
                <div className="text-gray-500 dark:text-gray-400 text-xs">Duration</div>
                <div className="font-semibold">{result.duration}</div>
              </div>
            )}
          </div>

          {/* Summaries */}
          {result.summaryEn && (
            <div className="mt-3 p-3 bg-white/40 dark:bg-black/20 rounded-lg">
              <p className="text-sm text-emerald-700 dark:text-emerald-300">
                {result.summaryEn}
              </p>
            </div>
          )}
          {result.summaryAr && (
            <div className="mt-2 p-3 bg-white/40 dark:bg-black/20 rounded-lg" dir="rtl">
              <p className="text-sm text-emerald-700 dark:text-emerald-300">
                {result.summaryAr}
              </p>
            </div>
          )}
        </div>
      )}

      {/* Error Result */}
      {result?.success === false && result.error && (
        <div className="bg-red-100 dark:bg-red-900/40 border border-red-300 dark:border-red-600 rounded-xl p-4">
          <div className="flex items-center gap-3 mb-3">
            <span className="text-2xl">❌</span>
            <div>
              <h4 className="font-semibold text-red-800 dark:text-red-200">
                Transaction Failed
              </h4>
              <p className="text-sm text-red-600 dark:text-red-300">
                فشلت المعاملة
              </p>
            </div>
          </div>
          <div className="bg-white/60 dark:bg-black/20 rounded-lg p-3 space-y-2 text-sm">
            {result.error.failedStep && (
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Failed Step:</span>
                <span className="text-red-700 dark:text-red-300 font-medium">
                  {result.error.failedStep}
                </span>
              </div>
            )}
            {result.error.message && (
              <div>
                <span className="text-gray-600 dark:text-gray-400">Error:</span>
                <p className="text-red-700 dark:text-red-300 mt-1">
                  {result.error.message}
                </p>
              </div>
            )}
            {result.error.errorCode && (
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Error Code:</span>
                <code className="bg-red-200 dark:bg-red-800 px-2 py-0.5 rounded text-xs">
                  {result.error.errorCode}
                </code>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Current Step Indicator */}
      {currentStep && !result && (
        <div className="flex items-center gap-2 text-sm text-teal-600 dark:text-teal-400 animate-pulse">
          <span className="inline-block animate-spin">⏳</span>
          <span>Processing: {currentStep.step}...</span>
        </div>
      )}
    </div>
  );
};

export default FundInWorkflowCard;
