import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SeasonalProduceResponse } from '../models/seasonal-produce.models';

@Injectable({ providedIn: 'root' })
export class SeasonalProduceService {
  private readonly baseUrl = `${environment.apiUrl}/v1/seasonal-produce`;
  private readonly http = inject(HttpClient);

  getAllWindows(): Observable<SeasonalProduceResponse[]> {
    return this.http.get<SeasonalProduceResponse[]>(`${this.baseUrl}/all`);
  }

  getInSeason(): Observable<SeasonalProduceResponse[]> {
    return this.http.get<SeasonalProduceResponse[]>(this.baseUrl);
  }
}
