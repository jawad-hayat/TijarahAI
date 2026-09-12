import { Pipe, PipeTransform, inject } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { marked } from 'marked';

@Pipe({
  name: 'markdown',
  standalone: true
})
export class MarkdownPipe implements PipeTransform {
  private sanitizer = inject(DomSanitizer);

  transform(value: string | null | undefined): SafeHtml {
    if (!value) return '';

    let cleaned = value;

    // Normalize LaTeX math symbols: $\le 0.0\%$ -> ≤ 0.0%
    cleaned = cleaned.replace(/\$\s*\\le\s*([0-9.]+%?)\s*\$/g, '≤ $1');
    cleaned = cleaned.replace(/\$\s*\\ge\s*([0-9.]+%?)\s*\$/g, '≥ $1');
    cleaned = cleaned.replace(/\\le/g, '≤');
    cleaned = cleaned.replace(/\\ge/g, '≥');

    // Normalize currency placeholder ¤ to $
    cleaned = cleaned.replace(/¤/g, '$');

    // Parse markdown to HTML
    let html = marked.parse(cleaned, { async: false }) as string;

    // Enhance Shariah statuses with badges
    html = html.replace(
      /<strong>\s*NON-COMPLIANT\s*<\/strong>/gi,
      '<span class="px-2.5 py-0.5 rounded-full bg-red-950/80 border border-red-500/50 text-red-400 font-bold text-xs inline-flex items-center space-x-1"><span>❌</span><span>NON-COMPLIANT</span></span>'
    );
    html = html.replace(
      /<strong>\s*COMPLIANT\s*<\/strong>/gi,
      '<span class="px-2.5 py-0.5 rounded-full bg-emerald-950/80 border border-emerald-500/50 text-emerald-400 font-bold text-xs inline-flex items-center space-x-1"><span>✅</span><span>COMPLIANT</span></span>'
    );
    html = html.replace(
      /<strong>\s*FAILED\s*<\/strong>/gi,
      '<span class="text-red-400 font-bold">FAILED ❌</span>'
    );
    html = html.replace(
      /<strong>\s*PASS\s*<\/strong>/gi,
      '<span class="text-emerald-400 font-bold">PASS ✅</span>'
    );

    return this.sanitizer.bypassSecurityTrustHtml(html);
  }
}
