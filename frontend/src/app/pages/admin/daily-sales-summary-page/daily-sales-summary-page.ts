import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SalesService } from '../../../core/services/sales';
import { ToastService } from '../../../core/services/toast';
import { DailySalesSummaryDto } from '../../../core/models';

@Component({
  selector: 'app-daily-sales-summary-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './daily-sales-summary-page.html',
  styleUrls: ['./daily-sales-summary-page.scss']
})
export class DailySalesSummaryPage implements OnInit {
  private readonly salesService = inject(SalesService);
  private readonly toastService = inject(ToastService);

  summary: DailySalesSummaryDto | null = null;
  isLoading = false;
  selectedDate = new Date().toISOString().split('T')[0];

  ngOnInit(): void {
    this.loadSummary();
  }

  loadSummary(): void {
    this.isLoading = true;
    this.salesService.getDailySummary(this.selectedDate).subscribe({
      next: (response) => {
        this.summary = response;
        this.isLoading = false;
      },
      error: () => {
        this.toastService.error('Error', 'Failed to load daily summary');
        this.isLoading = false;
      }
    });
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      minimumFractionDigits: 2
    }).format(value);
  }

  getPaymentModes(): Array<[string, number]> {
    return this.summary ? Object.entries(this.summary.byPaymentMode) : [];
  }

  getCategories(): Array<[string, number]> {
    return this.summary ? Object.entries(this.summary.byCategoryName) : [];
  }

  getTopPaymentMode(): string {
    const modes = this.getPaymentModes();
    return modes.length > 0 ? modes.reduce((a, b) => b[1] > a[1] ? b : a)[0] : '—';
  }

  getTopCategory(): string {
    const categories = this.getCategories();
    return categories.length > 0 ? categories.reduce((a, b) => b[1] > a[1] ? b : a)[0] : '—';
  }

  getPaymentModePercentage(mode: string, amount: number): number {
    if (!this.summary || this.summary.totalAmountReceived === 0) return 0;
    return (amount / this.summary.totalAmountReceived) * 100;
  }

  getCategoryPercentage(category: string, amount: number): number {
    if (!this.summary || this.summary.totalNetAmount === 0) return 0;
    return (amount / this.summary.totalNetAmount) * 100;
  }
}
