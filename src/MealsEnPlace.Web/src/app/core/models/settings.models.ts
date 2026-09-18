export type ClaudeModel = 'Fable51' | 'Haiku45' | 'Opus5' | 'Sonnet5';

export interface ClaudeTokenStatusResponse {
  configured: boolean;
  model: ClaudeModel;
}

export interface ClaudeTokenTestResponse {
  errorMessage?: string | null;
  success: boolean;
}

export interface SaveClaudeModelRequest {
  model: ClaudeModel;
}

export interface SaveClaudeTokenRequest {
  token: string;
}

export interface SaveTodoistTokenRequest {
  token: string;
}

export interface TestClaudeTokenRequest {
  token?: string | null;
}

export interface TestTodoistTokenRequest {
  token?: string | null;
}

export interface TodoistTokenTestResponse {
  errorMessage?: string | null;
  success: boolean;
}
