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
