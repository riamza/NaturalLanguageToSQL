export interface QueryResponse {
  data: any[];
  generatedSql: string;
  executionTimeMs: number;
  historyId: string;
  requiresApproval?: boolean;
  ir?: any;
  originalPrompt?: string;
}

export interface QueryHistoryItem {
  id: string;
  prompt: string;
  generatedSql: string;
  executionTimeMs: number;
  isSuccessful: boolean;
  errorMessage: string;
  userFeedback: string;
  createdAtUtc: string;
}
