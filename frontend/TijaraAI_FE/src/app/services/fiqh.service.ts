import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { FiqhQueryRequest, FiqhQueryResponse } from '../models/fiqh.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class FiqhService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/api/Fiqh`;

  askFiqhQuestion(request: FiqhQueryRequest): Observable<FiqhQueryResponse> {
    return this.http.post<FiqhQueryResponse>(`${this.apiUrl}/ask`, request);
  }
}
