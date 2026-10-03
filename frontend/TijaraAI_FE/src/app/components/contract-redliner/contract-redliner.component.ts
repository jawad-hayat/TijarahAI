import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AgentService } from '../../services/agent.service';
import { ContractAuditResponse } from '../../models/agent.model';

interface ContractSample { name: string; type: string; text: string; }

@Component({
  selector: 'app-contract-redliner', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './contract-redliner.component.html'
})
export class ContractRedlinerComponent {
  private agentService = inject(AgentService);
  readonly maxContractLength = 15_000;
  contractType = signal('Freelance Agreement');
  contractText = signal(`1. The Contractor agrees to deliver web development services by November 1st.
2. In the event of payment delay past 30 days, the Client shall pay a late penalty fee of 5% per month on the overdue balance.
3. The Contractor guarantees a 20% return on the Client's capital contribution regardless of market outcomes.`);
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  auditResult = signal<ContractAuditResponse | null>(null);
  copyLabel = signal('Copy remediated contract');

  sampleTemplates: ContractSample[] = [
    { name: 'Freelance late fee', type: 'Freelance Agreement', text: 'Section 4: If invoices remain unpaid after 15 days, a late payment fee of 3% per week will be added to the balance.' },
    { name: 'Dropshipping resale', type: 'E-commerce Terms', text: 'Clause 2: The Seller agrees to market and sell inventory prior to purchasing from the manufacturer, retaining 100% of profit.' },
    { name: 'Capital guarantee', type: 'Musharakah Agreement', text: "Clause 8: Partner A invests capital while Partner B manages operations. Partner B guarantees to return 100% of Partner A's investment even if the company experiences a total loss." }
  ];

  loadSample(sample: ContractSample): void { this.contractType.set(sample.type); this.contractText.set(sample.text); this.runAudit(); }

  runAudit(): void {
    const contractText = this.contractText().trim();
    if (!contractText || this.isLoading()) return;
    this.isLoading.set(true); this.errorMessage.set(null); this.auditResult.set(null); this.copyLabel.set('Copy remediated contract');
    this.agentService.auditContract(contractText, this.contractType().trim() || 'General Commercial').subscribe({
      next: result => { this.auditResult.set(result); this.isLoading.set(false); },
      error: error => { this.errorMessage.set(error?.error?.error || 'Unable to audit this contract. Ensure the Agent API is running.'); this.isLoading.set(false); }
    });
  }

  async copyRemediatedContract(contract: string): Promise<void> {
    try { await navigator.clipboard.writeText(contract); this.copyLabel.set('Copied'); }
    catch { this.copyLabel.set('Select text to copy'); }
  }
}
