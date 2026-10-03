import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ContractAuditResponse, WebResearchResponse } from '../models/agent.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AgentService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.agentApiUrl}/api/Agent`;

  auditContract(contractText: string, contractType: string): Observable<ContractAuditResponse> {
    return this.http.post<ContractAuditResponse>(`${this.apiUrl}/audit-contract`, { contractText, contractType });
  }

  runWebResearch(query: string): Observable<WebResearchResponse> {
    return this.http.post<WebResearchResponse>(`${this.apiUrl}/web-research`, { query });
  }
}
