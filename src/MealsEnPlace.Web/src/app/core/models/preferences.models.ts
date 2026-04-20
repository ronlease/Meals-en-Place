export type DisplaySystem = 'Imperial' | 'Metric';

export interface UserPreferencesResponse {
  autoDepleteOnConsume: boolean;
  displaySystem: DisplaySystem;
}

export interface UpdateUserPreferencesRequest {
  autoDepleteOnConsume?: boolean;
  displaySystem: DisplaySystem;
}
