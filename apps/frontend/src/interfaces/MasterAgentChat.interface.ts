import { WorkflowStep } from '../components/Cards/WorkflowProgressCard';
import { UploadedFile } from '../components/FileUpload';
import { ProjectionResult } from '../services/masterAgent';

export interface ChatMessage {
  id: string;
  type: 'user' | 'agent' | 'thinking' | 'delegation' | 'tool' | 'telemetry' | 'multimodal' | 'transcription' | 'projection' | 'workflow-progress';
  content: string;
  timestamp: Date;
  subAgentName?: string;
  toolName?: string;
  metadata?: Record<string, unknown>;
  files?: UploadedFile[]; // For displaying user's uploaded files
  projectionResult?: ProjectionResult; // For structured projection data
  workflowSteps?: WorkflowStep[]; // For workflow progress tracking
}

export interface TelemetryData {
  traceId?: string;
  duration?: number;
  delegationCount?: number;
  subAgentsUsed?: string[];
  toolsUsed?: string[];
}
