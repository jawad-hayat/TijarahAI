import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FiqhService } from '../../services/fiqh.service';
import { FiqhQueryResponse } from '../../models/fiqh.model';
import { WebResearchResponse } from '../../models/agent.model';
import { AgentService } from '../../services/agent.service';
import { MarkdownPipe } from '../../pipes/markdown.pipe';

@Component({
  selector: 'app-fiqh-advisor',
  standalone: true,
  imports: [CommonModule, FormsModule, MarkdownPipe],
  templateUrl: './fiqh-advisor.component.html'
})
export class FiqhAdvisorComponent {
  private fiqhService = inject(FiqhService);
  private agentService = inject(AgentService);

  question = signal<string>('Can I do dropshipping where I sell goods to a customer before buying or possessing them?');
  isLoading = signal<boolean>(false);
  errorMessage = signal<string | null>(null);
  fiqhResponse = signal<FiqhQueryResponse | null>(null);
  webResearchResponse = signal<WebResearchResponse | null>(null);
  isWebResearch = signal<boolean>(false);

  sampleQuestions = [
    'Can I do dropshipping where I sell goods before taking ownership?',
    'Is it permissible to charge late payment penalty fees on commercial invoices?',
    'What are the Shariah requirements for earning affiliate marketing commissions?',
    'Can partners agree to a fixed guaranteed return in a Musharakah?'
  ];

  selectSampleQuestion(q: string) {
    this.question.set(q);
    this.askAdvisor();
  }

  askAdvisor() {
    const question = this.question().trim();
    if (!question || this.isLoading()) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.fiqhResponse.set(null);
    this.webResearchResponse.set(null);
    this.isWebResearch.set(false);

    this.fiqhService.askFiqhQuestion({ question }).subscribe({
      next: (res) => {
        if (this.hasNoRagAnswer(res)) {
          // The RAG API returns HTTP 200 with this explicit state when both knowledge bases lack evidence.
          this.runWebResearch(true);
          return;
        }
        this.fiqhResponse.set(res);
        this.isLoading.set(false);
      },
      error: () => {
        // The agent endpoint is the free Google Search-grounded fallback when RAG is unavailable.
        this.runWebResearch(true);
      }
    });
  }

  runWebResearch(isFallback = false) {
    const question = this.question().trim();
    if (!question || (this.isLoading() && !isFallback)) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.isWebResearch.set(true);
    if (!isFallback) {
      this.fiqhResponse.set(null);
      this.webResearchResponse.set(null);
    }

    this.agentService.runWebResearch(question).subscribe({
      next: result => {
        this.webResearchResponse.set(result);
        this.isLoading.set(false);
      },
      error: err => {
        const prefix = isFallback ? 'RAG was unavailable and the web-research fallback also failed. ' : 'Web research failed. ';
        this.errorMessage.set(prefix + (err?.error?.error || 'Ensure the local Agent API is running at https://localhost:7044.'));
        this.isLoading.set(false);
      }
    });
  }

  private hasNoRagAnswer(response: FiqhQueryResponse): boolean {
    const unavailableRulings = [
      'no relevant evidence found',
      'unable to generate a ruling',
      'indeterminate'
    ];
    const rulings = [response.hanafiPerspective?.ruling, response.ahleHadithPerspective?.ruling]
      .map(ruling => (ruling || '').trim().toLowerCase());

    return rulings.length === 2 && rulings.every(ruling => unavailableRulings.includes(ruling));
  }
}
