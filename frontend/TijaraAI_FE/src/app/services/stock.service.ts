import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StockCheckRequest, StockCheckResponse } from '../models/stock.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class StockService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/api/Stock`;

  checkStock(request: StockCheckRequest): Observable<StockCheckResponse> {
    return this.http.post<StockCheckResponse>(`${this.apiUrl}/check`, request);
  }
}
