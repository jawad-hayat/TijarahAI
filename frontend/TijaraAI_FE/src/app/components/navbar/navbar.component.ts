import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <header class="border-b border-slate-800/80 bg-slate-950/80 backdrop-blur-md sticky top-0 z-50">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-3 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between lg:gap-6">

        <!-- Logo & Title -->
        <a href="/"
           (click)="$event.preventDefault(); tabChanged.emit('stock')"
           aria-label="TijarahAI home"
           class="flex items-center gap-3 min-w-0 rounded-xl cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500/60">
          <div class="w-10 h-10 shrink-0 rounded-xl bg-gradient-to-br from-brand-600 to-emerald-400 flex items-center justify-center shadow-lg shadow-brand-600/30 ring-1 ring-white/10">
            <span class="text-xl font-bold text-white font-serif">ت</span>
          </div>
          <div class="min-w-0">
            <div class="flex flex-wrap items-center gap-x-2 gap-y-1">
              <span class="text-lg font-bold text-white tracking-tight leading-none">Tijarah<span class="text-brand-500">AI</span></span>
              <span class="inline-flex items-center gap-1.5 text-[10px] uppercase tracking-wider font-semibold px-2 py-0.5 rounded-full bg-brand-500/10 text-brand-400 border border-brand-500/20">
                <span class="w-1.5 h-1.5 rounded-full bg-brand-500 animate-pulse"></span>
                Free RAG MVP
              </span>
            </div>
            <p class="mt-1 text-xs text-slate-400 leading-snug hidden sm:block truncate">Evidence-Based Islamic Commerce &amp; Wealth Intelligence</p>
          </div>
        </a>

        <!-- Navigation Tabs -->
        <nav aria-label="Main navigation"
             class="grid grid-cols-3 gap-1 w-full p-1 rounded-xl bg-slate-900 border border-slate-800 text-sm lg:flex lg:w-auto lg:items-center">

          <button
            type="button"
            (click)="tabChanged.emit('stock')"
            [attr.aria-current]="activeTab() === 'stock' ? 'page' : null"
            [ngClass]="activeTab() === 'stock' ? 'bg-brand-600 text-white shadow-lg shadow-brand-600/20' : 'text-slate-400 hover:text-slate-100 hover:bg-slate-800/70'"
            class="px-2 py-2 sm:px-4 rounded-lg font-medium transition-all duration-200 flex flex-col sm:flex-row items-center justify-center gap-1 sm:gap-2 text-[11px] sm:text-sm leading-tight text-center whitespace-nowrap focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500/60">
            <svg class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M3 3v18h18"/>
              <path d="M7 15v3"/>
              <path d="M12 10v8"/>
              <path d="M17 6v12"/>
            </svg>
            <span>Equity Screener</span>
          </button>

          <button
            type="button"
            (click)="tabChanged.emit('fiqh')"
            [attr.aria-current]="activeTab() === 'fiqh' ? 'page' : null"
            [ngClass]="activeTab() === 'fiqh' ? 'bg-brand-600 text-white shadow-lg shadow-brand-600/20' : 'text-slate-400 hover:text-slate-100 hover:bg-slate-800/70'"
            class="px-2 py-2 sm:px-4 rounded-lg font-medium transition-all duration-200 flex flex-col sm:flex-row items-center justify-center gap-1 sm:gap-2 text-[11px] sm:text-sm leading-tight text-center whitespace-nowrap focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500/60">
            <svg class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M12 3v18"/>
              <path d="M8 21h8"/>
              <path d="M4 7h16"/>
              <path d="M4 7l-2.5 6a3.5 3.5 0 0 0 5 0L4 7z"/>
              <path d="M20 7l-2.5 6a3.5 3.5 0 0 0 5 0L20 7z"/>
            </svg>
            <span>Dual-Fiqh Advisor</span>
          </button>

          <button
            type="button"
            (click)="tabChanged.emit('contract')"
            [attr.aria-current]="activeTab() === 'contract' ? 'page' : null"
            [ngClass]="activeTab() === 'contract' ? 'bg-brand-600 text-white shadow-lg shadow-brand-600/20' : 'text-slate-400 hover:text-slate-100 hover:bg-slate-800/70'"
            class="px-2 py-2 sm:px-4 rounded-lg font-medium transition-all duration-200 flex flex-col sm:flex-row items-center justify-center gap-1 sm:gap-2 text-[11px] sm:text-sm leading-tight text-center whitespace-nowrap focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500/60">
            <svg class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8l-5-5z"/>
              <path d="M14 3v5h5"/>
              <path d="M9 13h6"/>
              <path d="M9 17h6"/>
            </svg>
            <span>Contract Red-Liner</span>
          </button>
        </nav>

      </div>
    </header>
  `
})
export class NavbarComponent {
  activeTab = input.required<'stock' | 'fiqh' | 'contract'>();
  tabChanged = output<'stock' | 'fiqh' | 'contract'>();
}