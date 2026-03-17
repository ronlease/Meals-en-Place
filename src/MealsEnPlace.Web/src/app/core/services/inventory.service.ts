import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddInventoryItemRequest,
  ContainerReferenceDetectedResponse,
  InventoryItemResponse,
  InventoryLocation,
  UpdateInventoryItemRequest,
} from '../models/inventory.models';

@Injectable({ providedIn: 'root' })
export class InventoryService {
  private readonly baseUrl = `${environment.apiUrl}/v1/inventory`;
  private readonly http = inject(HttpClient);

  addItem(
    request: AddInventoryItemRequest
  ): Observable<InventoryItemResponse | ContainerReferenceDetectedResponse> {
    return this.http.post<
      InventoryItemResponse | ContainerReferenceDetectedResponse
    >(this.baseUrl, request, { observe: 'body' });
  }

  deleteItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  getItems(location: InventoryLocation): Observable<InventoryItemResponse[]> {
    return this.http.get<InventoryItemResponse[]>(this.baseUrl, {
      params: { location },
    });
  }

  updateItem(
    id: string,
    request: UpdateInventoryItemRequest
  ): Observable<InventoryItemResponse> {
    return this.http.put<InventoryItemResponse>(
      `${this.baseUrl}/${id}`,
      request
    );
  }
}
