// ----------------------------------------------------------------
// Search / Import DTOs
// ----------------------------------------------------------------

export interface RecipeSearchResultDto {
  alreadyImported: boolean;
  category: string;
  id: string;
  thumbnail: string | null;
  title: string;
}

export interface RecipeImportResultDto {
  dietaryTags: DietaryTag[];
  recipeId: string;
  title: string;
  totalIngredients: number;
  unresolvedCount: number;
}

// ----------------------------------------------------------------
// Recipe library list DTO
// ----------------------------------------------------------------

export interface RecipeListItemDto {
  cuisineType: string;
  dietaryTags: DietaryTag[];
  id: string;
  ingredientNames: string[];
  isFullyResolved: boolean;
  title: string;
  totalIngredients: number;
  unresolvedCount: number;
}

// ----------------------------------------------------------------
// Recipe matching DTOs
// ----------------------------------------------------------------

export type MatchTier = 'FullMatch' | 'NearMatch' | 'PartialMatch';

export interface MatchedIngredientDto {
  availableQuantity: number;
  availableUom: string;
  ingredientName: string;
  isExpiryImminent: boolean;
  requiredQuantity: number;
  requiredUom: string;
}

export interface MissingIngredientDto {
  ingredientName: string;
  requiredQuantity: number;
  requiredUom: string;
}

export type ClaudeConfidence = 'High' | 'Medium' | 'Low';

export interface SubstitutionSuggestion {
  confidence: ClaudeConfidence;
  missingIngredientName: string;
  notes: string;
  suggestedSubstitute: string;
}

export interface RecipeMatchDto {
  cuisineType: string;
  finalScore: number;
  matchedIngredients: MatchedIngredientDto[];
  matchScore: number;
  matchTier: MatchTier;
  missingIngredients: MissingIngredientDto[];
  recipeId: string;
  substitutionSuggestions: SubstitutionSuggestion[];
  title: string;
}

export interface RecipeMatchResponse {
  fullMatches: RecipeMatchDto[];
  nearMatches: RecipeMatchDto[];
  partialMatches: RecipeMatchDto[];
}

// ----------------------------------------------------------------
// Dietary tag enum
// ----------------------------------------------------------------

export type DietaryTag =
  | 'Vegetarian'
  | 'Vegan'
  | 'Carnivore'
  | 'LowCarb'
  | 'GlutenFree'
  | 'DairyFree';
