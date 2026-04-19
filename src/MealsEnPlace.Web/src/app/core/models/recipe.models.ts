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
// Recipe detail DTOs
// ----------------------------------------------------------------

export interface RecipeDetailDto {
  cuisineType: string;
  dietaryTags: string[];
  id: string;
  ingredients: RecipeIngredientDetailDto[];
  instructions: string;
  isFullyResolved: boolean;
  servingCount: number;
  sourceUrl: string | null;
  title: string;
}

export interface RecipeIngredientDetailDto {
  canonicalIngredientId: string;
  id: string;
  ingredientName: string;
  isContainerResolved: boolean;
  notes: string | null;
  quantity: number;
  unitOfMeasureAbbreviation: string;
  unitOfMeasureId: string | null;
}

export interface CreateRecipeRequest {
  cuisineType: string;
  ingredients: CreateRecipeIngredientRequest[];
  instructions: string;
  servingCount: number;
  title: string;
}

export interface CreateRecipeIngredientRequest {
  canonicalIngredientId: string;
  notes?: string | null;
  quantity: number;
  unitOfMeasureId?: string | null;
}

// ----------------------------------------------------------------
// Recipe matching DTOs
// ----------------------------------------------------------------

export type MatchTier = 'FullMatch' | 'NearMatch' | 'PartialMatch';

export interface MatchedIngredientDto {
  availableQuantity: number;
  availableUnitOfMeasure: string;
  ingredientName: string;
  isExpiryImminent: boolean;
  requiredQuantity: number;
  requiredUnitOfMeasure: string;
}

export interface MissingIngredientDto {
  ingredientName: string;
  requiredQuantity: number;
  requiredUnitOfMeasure: string;
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
  claudeFeasibilityApplied: boolean;
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

// ----------------------------------------------------------------
// Container resolution (MEP-003 single + MEP-026 grouped)
// ----------------------------------------------------------------

export interface UnresolvedGroupResponse {
  canonicalIngredientId: string;
  canonicalIngredientName: string;
  notes: string;
  occurrenceCount: number;
}

export interface BulkResolveGroupRequest {
  canonicalIngredientId: string;
  notes: string;
  quantity: number;
  unitOfMeasureId: string;
}

export interface BulkResolveGroupResponse {
  affectedCount: number;
}
