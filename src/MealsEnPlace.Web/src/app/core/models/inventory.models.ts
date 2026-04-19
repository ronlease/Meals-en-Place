export interface CanonicalIngredientDto {
  category: string;
  defaultUnitOfMeasureId: string;
  id: string;
  name: string;
}

export interface CreateIngredientRequest {
  category: string;
  defaultUnitOfMeasureId: string;
  name: string;
}

export type InventoryLocation = 'Pantry' | 'Fridge' | 'Freezer';

export interface UnitOfMeasureDto {
  abbreviation: string;
  id: string;
  name: string;
  unitOfMeasureType: string;
}

export interface AddInventoryItemRequest {
  canonicalIngredientId: string;
  declaredQuantity?: number | null;
  declaredUnitOfMeasureId?: string | null;
  expiryDate: string | null;
  location: InventoryLocation;
  notes: string;
  quantity: number;
  unitOfMeasureId: string;
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
  unitOfMeasureAbbreviation: string;
  unitOfMeasureId: string;
}

export interface UpdateInventoryItemRequest {
  expiryDate: string | null;
  location: InventoryLocation;
  notes?: string | null;
  quantity: number;
  unitOfMeasureId: string;
}
