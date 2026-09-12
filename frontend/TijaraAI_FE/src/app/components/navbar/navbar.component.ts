import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <header class="border-b border-slate-800/80 bg-slate-950/80 backdrop-blur-md sticky top-0 z-50">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-18 flex items-center justify-between py-3">

        <!-- Logo & Title -->
        <div class="flex items-center space-x-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-brand-600 to-emerald-400 flex items-center justify-center shadow-lg shadow-brand-600/30 transition-transform hover:scale-110">
            <span class="text-xl font-bold text-white font-serif">ت</span>
          </div>
          <div>
            <div class="flex items-center space-x-2">
              <span class="text-lg font-bold text-white tracking-tight">Tijarah<span class="text-brand-500">AI</span></span>
              <span class="text-[10px] uppercase font-semibold px-2 py-0.5 rounded-full bg-brand-500/10 text-brand-400 border border-brand-500/20 animate-pulse">Free RAG MVP</span>
            </div>
            <p class="text-xs text-slate-400 hidden sm:block">Evidence-Based Islamic Commerce & Wealth Intelligence</p>
          </div>
        </div>

        <!-- Navigation Tabs -->
        <div class="flex items-center p-1 rounded-xl bg-slate-900 border border-slate-800 text-sm">
          <button
            (click)="tabChanged.emit('stock')"
            [ngClass]="activeTab() === 'stock' ? 'bg-brand-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'"
            class="px-4 py-2 rounded-lg font-medium transition-all duration-300 flex items-center space-x-2">
            <span>📊</span>
            <span class="hidden sm:inline">Equity Screener</span>
          </button>

          <button
            (click)="tabChanged.emit('fiqh')"
            [ngClass]="activeTab() === 'fiqh' ? 'bg-brand-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'"
            class="px-4 py-2 rounded-lg font-medium transition-all duration-300 flex items-center space-x-2">
            <span>⚖️</span>
            <span class="hidden sm:inline">Dual-Fiqh Advisor</span>
          </button>
        </div>

      </div>
    </header>
  `
})
export class NavbarComponent {
  activeTab = input.required<'stock' | 'fiqh'>();
  tabChanged = output<'stock' | 'fiqh'>();
}
