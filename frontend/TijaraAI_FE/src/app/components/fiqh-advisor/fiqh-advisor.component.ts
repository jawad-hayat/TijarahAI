import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FiqhService } from '../../services/fiqh.service';
import { FiqhQueryResponse } from '../../models/fiqh.model';
import { MarkdownPipe } from '../../pipes/markdown.pipe';

@Component({
  selector: 'app-fiqh-advisor',
  standalone: true,
  imports: [CommonModule, FormsModule, MarkdownPipe],
  templateUrl: './fiqh-advisor.component.html'
})
export class FiqhAdvisorComponent {
  private fiqhService = inject(FiqhService);

  question = signal<string>('Can I do dropshipping where I sell goods to a customer before buying or possessing them?');
  isLoading = signal<boolean>(false);
  errorMessage = signal<string | null>(null);
  fiqhResponse = signal<FiqhQueryResponse | null>(null);

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
    if (!this.question().trim()) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.fiqhService.askFiqhQuestion({ question: this.question().trim() }).subscribe({
      next: (res) => {
        this.fiqhResponse.set(res);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.error || 'Failed to query RAG fiqh knowledge bases.');
        this.isLoading.set(false);
      }
    });
  }
}
