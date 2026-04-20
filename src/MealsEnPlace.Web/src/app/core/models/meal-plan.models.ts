import { DietaryTag } from './recipe.models';

export type MealSlot = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack';

export interface GenerateMealPlanRequest {
  dietaryTags?: DietaryTag[];
  name?: string;
  seasonalOnly?: boolean;
  slotPreferences?: Record<string, MealSlot[]>;
  weekStartDate?: string;
}

export interface MealPlanResponse {
  createdAt: string;
  id: string;
  name: string;
  slots: MealPlanSlotResponse[];
  weekStartDate: string;
}

export interface MealPlanSlotResponse {
  consumedAt: string | null;
  cuisineType: string;
  dayOfWeek: string;
  id: string;
  mealSlot: MealSlot;
  recipeId: string;
  recipeTitle: string;
}

export interface SwapSlotRequest {
  recipeId: string;
}

export interface ConsumeMealResponse {
  autoDepleteApplied: boolean;
  consumedAt: string;
  shortIngredients: ShortIngredientResponse[];
}

export interface ShortIngredientResponse {
  ingredientName: string;
  shortBy: number;
  unitOfMeasureAbbreviation: string;
}

export interface ReorderedSlotDto {
  id: string;
  mealSlot: MealSlot;
  originalDay: string;
  proposedDay: string;
  recipeId: string;
  recipeTitle: string;
  urgencyScore: number;
}

export interface ReorderPreviewResponse {
  changes: ReorderedSlotDto[];
  hasChanges: boolean;
  reason: string | null;
  urgencyWindowDays: number;
}
