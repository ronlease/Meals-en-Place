export interface WasteAlertRecipeDto {
  cuisineType: string;
  recipeId: string;
  title: string;
}

export interface WasteAlertResponse {
  alertId: string;
  canonicalIngredientName: string;
  createdAt: string;
  daysUntilExpiry: number;
  expiryDate: string;
  inventoryItemId: string;
  location: string;
  matchedRecipes: WasteAlertRecipeDto[];
  quantity: number;
  uomAbbreviation: string;
}
