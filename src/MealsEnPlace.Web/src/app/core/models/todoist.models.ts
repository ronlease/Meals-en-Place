export interface MealPlanPushResult {
  closed: number;
  created: number;
  unchanged: number;
  updated: number;
}

export interface ShoppingListPushResult {
  closed: number;
  created: number;
  unchanged: number;
  updated: number;
}

export interface TodoistProjectEntry {
  displayName: string | null;
  isInbox: boolean;
  projectId: string | null;
}

export interface TodoistProjectHistoryResponse {
  lastUsedMealPlanProjectId: string | null;
  lastUsedShoppingListProjectId: string | null;
  nameResolutionError: string | null;
  namesResolved: boolean;
  projects: TodoistProjectEntry[];
}

/** Identifies which push surface a project selection belongs to. */
export type TodoistPushResourceType = 'mealPlan' | 'shoppingList';

export interface TodoistStatusResponse {
  configured: boolean;
}
