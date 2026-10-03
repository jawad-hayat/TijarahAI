import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavbarComponent } from './components/navbar/navbar.component';
import { StockScreenerComponent } from './components/stock-screener/stock-screener.component';
import { FiqhAdvisorComponent } from './components/fiqh-advisor/fiqh-advisor.component';
import { ContractRedlinerComponent } from './components/contract-redliner/contract-redliner.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, NavbarComponent, StockScreenerComponent, FiqhAdvisorComponent, ContractRedlinerComponent],
  template: `
    <div class="min-h-screen flex flex-col justify-between">
      <div>
        <app-navbar [activeTab]="currentTab()" (tabChanged)="onTabChanged($event)"></app-navbar>

        <main class="py-8 px-4 sm:px-6 lg:px-8">
          <app-stock-screener *ngIf="currentTab() === 'stock'"></app-stock-screener>
          <app-fiqh-advisor *ngIf="currentTab() === 'fiqh'"></app-fiqh-advisor>
          <app-contract-redliner *ngIf="currentTab() === 'contract'"></app-contract-redliner>
        </main>
      </div>

      <!-- Footer -->
      <footer class="border-t border-slate-800/60 py-6 text-center text-xs text-slate-500">
        <p>TijarahAI © 2026 — Evidence-Based Islamic Wealth & Commerce Intelligence. For formal contracts, verify with qualified scholars.</p>
      </footer>
    </div>
  `
})
export class AppComponent {
  currentTab = signal<'stock' | 'fiqh' | 'contract'>('stock');

  onTabChanged(tab: 'stock' | 'fiqh' | 'contract') {
    this.currentTab.set(tab);
  }
}
