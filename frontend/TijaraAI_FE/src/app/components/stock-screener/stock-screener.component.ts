import { Component, inject, signal, AfterViewInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StockService } from '../../services/stock.service';
import { ComplianceStatus, StockCheckResponse } from '../../models/stock.model';

import { MarkdownPipe } from '../../pipes/markdown.pipe';

interface QuickStock {
  symbol: string;
  name: string;
  sector: string;
  tvSymbol: string;
  accent: string;
}

@Component({
  selector: 'app-stock-screener',
  standalone: true,
  imports: [CommonModule, FormsModule, MarkdownPipe],
  templateUrl: './stock-screener.component.html'
})
export class StockScreenerComponent implements AfterViewInit {
  private stockService = inject(StockService);

  @ViewChild('tradingViewWidget', { static: false }) tradingViewWidget?: ElementRef;
  @ViewChild('tickerTapeContainer', { static: false }) tickerTapeContainer?: ElementRef;

  ticker = signal<string>('AAPL');
  isLoading = signal<boolean>(false);
  errorMessage = signal<string | null>(null);
  stockResult = signal<StockCheckResponse | null>(null);
  activeAuditTab = signal<'aaoifi' | 'strict'>('aaoifi');

  ComplianceStatus = ComplianceStatus;
  Math = Math;

  // Curated live symbols for online trading & Islamic finance screening
  popularStocks: QuickStock[] = [
    { symbol: 'AAPL', name: 'Apple Inc.', sector: 'Technology', tvSymbol: 'NASDAQ:AAPL', accent: 'border-emerald-500/40' },
    { symbol: 'NVDA', name: 'NVIDIA Corp.', sector: 'Semiconductors', tvSymbol: 'NASDAQ:NVDA', accent: 'border-green-500/40' },
    { symbol: 'MSFT', name: 'Microsoft', sector: 'Software & Cloud', tvSymbol: 'NASDAQ:MSFT', accent: 'border-blue-500/40' },
    { symbol: '2222.SR', name: 'Saudi Aramco', sector: 'Energy & Oil', tvSymbol: 'TADAWUL:2222', accent: 'border-gold-500/40' },
    { symbol: 'TSLA', name: 'Tesla Inc.', sector: 'Automotive & Clean Energy', tvSymbol: 'NASDAQ:TSLA', accent: 'border-red-500/40' },
    { symbol: 'AMZN', name: 'Amazon.com', sector: 'E-Commerce & Cloud', tvSymbol: 'NASDAQ:AMZN', accent: 'border-amber-500/40' }
  ];

  ngAfterViewInit() {
    this.initTickerTape();
    this.updateTradingViewChart(this.ticker());
    // Auto-run initial check for the default ticker
    this.runScreening();
  }

  onSelectStock(stock: QuickStock) {
    this.ticker.set(stock.symbol);
    this.updateTradingViewChart(stock.symbol);
    this.runScreening();
  }

  onSearchSubmit() {
    const symbol = this.ticker().trim().toUpperCase();
    if (!symbol) return;
    this.updateTradingViewChart(symbol);
    this.runScreening();
  }

  runScreening() {
    const symbol = this.ticker().trim().toUpperCase();
    if (!symbol) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.stockService.checkStock({
      ticker: symbol
    }).subscribe({
      next: (res) => {
        this.stockResult.set(res);
        this.isLoading.set(false);
      },
      error: (err) => {
        const detail = err?.error?.error || err?.message || 'Failed to connect to /api/Stock/check.';
        this.errorMessage.set(detail);
        this.isLoading.set(false);
      }
    });
  }

  // Map user input ticker to TradingView symbol
  mapToTradingViewSymbol(symbol: string): string {
    const s = symbol.trim().toUpperCase();
    if (s === '2222.SR' || s === '2222' || s === 'ARAMCO') return 'TADAWUL:2222';
    if (s.includes(':')) return s;
    return `NASDAQ:${s}`;
  }

  updateTradingViewChart(symbol: string) {
    if (!this.tradingViewWidget?.nativeElement) return;
    const container = this.tradingViewWidget.nativeElement;
    container.innerHTML = '';

    const widgetDiv = document.createElement('div');
    widgetDiv.className = 'tradingview-widget-container__widget';
    widgetDiv.style.height = '100%';
    widgetDiv.style.width = '100%';
    container.appendChild(widgetDiv);

    const script = document.createElement('script');
    script.src = 'https://s3.tradingview.com/external-embedding/embed-widget-advanced-chart.js';
    script.type = 'text/javascript';
    script.async = true;

    const tvSymbol = this.mapToTradingViewSymbol(symbol);

    script.innerHTML = JSON.stringify({
      autosize: true,
      symbol: tvSymbol,
      interval: 'D',
      timezone: 'Etc/UTC',
      theme: 'dark',
      style: '1',
      locale: 'en',
      backgroundColor: 'rgba(15, 23, 42, 0.95)',
      gridColor: 'rgba(30, 41, 59, 0.4)',
      hide_top_toolbar: false,
      hide_legend: false,
      save_image: false,
      calendar: false,
      hide_volume: false,
      support_host: 'https://www.tradingview.com'
    });

    container.appendChild(script);
  }

  initTickerTape() {
    if (!this.tickerTapeContainer?.nativeElement) return;
    const container = this.tickerTapeContainer.nativeElement;
    container.innerHTML = '';

    const widgetDiv = document.createElement('div');
    widgetDiv.className = 'tradingview-widget-container__widget';
    container.appendChild(widgetDiv);

    const script = document.createElement('script');
    script.src = 'https://s3.tradingview.com/external-embedding/embed-widget-ticker-tape.js';
    script.type = 'text/javascript';
    script.async = true;

    script.innerHTML = JSON.stringify({
      symbols: [
        { proName: 'NASDAQ:AAPL', title: 'Apple' },
        { proName: 'NASDAQ:NVDA', title: 'NVIDIA' },
        { proName: 'NASDAQ:MSFT', title: 'Microsoft' },
        { proName: 'TADAWUL:2222', title: 'Saudi Aramco' },
        { proName: 'NASDAQ:TSLA', title: 'Tesla' },
        { proName: 'NASDAQ:AMZN', title: 'Amazon' },
        { proName: 'FOREXCOM:SPXUSD', title: 'S&P 500' },
        { proName: 'OANDA:XAUUSD', title: 'Gold (Oz)' }
      ],
      showSymbolLogo: true,
      isTransparent: true,
      displayMode: 'adaptive',
      colorTheme: 'dark',
      locale: 'en'
    });

    container.appendChild(script);
  }
}
