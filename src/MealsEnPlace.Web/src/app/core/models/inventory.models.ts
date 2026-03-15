export interface CanonicalIngredientDto {
  abbreviation: string;
  category: string;
  defaultUomId: string;
  id: string;
  name: string;
}

export interface CreateIngredientRequest {
  category: string;
  defaultUomId: string;
  name: string;
}

export type InventoryLocation = 'Pantry' | 'Fridge' | 'Freezer';

export interface UnitOfMeasureDto {
  abbreviation: string;
  id: string;
  name: string;
  uomType: string;
}

export interface AddInventoryItemRequest {
  canonicalIngredientId: string;
  declaredQuantity?: number | null;
  declaredUomId?: string | null;
  expiryDate: string | null;
  location: InventoryLocation;
  notes: string;
  quantity: number;
  uomId: string;
}

export interface ContainerReferenceDetectedResponse {
  detectedKeyword: string;
  message: string;
  originalInput: string;
}

export interface InventoryItemResponse {
  canonicalIngredientId: string;
  canonicalIngredientName: string;
  expiryDate: string | null;
  id: string;
  location: InventoryLocation;
  notes: string | null;
  quantity: number;
  uomAbbreviation: string;
  uomId: string;
}

export interface UpdateInventoryItemRequest {
  expiryDate: string | null;
  location: InventoryLocation;
  notes?: string | null;
  quantity: number;
  uomId: string;
}
