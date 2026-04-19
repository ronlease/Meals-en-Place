export interface ClaudeTokenStatusResponse {
  configured: boolean;
}

export interface ClaudeTokenTestResponse {
  errorMessage?: string | null;
  success: boolean;
}

export interface SaveClaudeTokenRequest {
  token: string;
}

export interface TestClaudeTokenRequest {
  token?: string | null;
}
