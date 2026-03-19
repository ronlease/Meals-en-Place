export type DisplaySystem = 'Imperial' | 'Metric';

export interface UserPreferencesResponse {
  displaySystem: DisplaySystem;
}

export interface UpdateUserPreferencesRequest {
  displaySystem: DisplaySystem;
}
