import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SettingsService } from '../../../core/services/settings';
import { ToastService } from '../../../core/services/toast';

@Component({
  selector: 'app-reports-page',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './reports-page.html',
  styleUrl: './reports-page.scss'
})
export class ReportsPage {
  private readonly settings = inject(SettingsService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toasts = inject(ToastService);

  reportData: any = null;
  reportType = '';
  isLoading = false;
  errorMessage = '';

  readonly reportOptions = [
    { value: 'sales/summary', label: 'Sales Summary' },
    { value: 'sales/products', label: 'Product-wise Sales' },
    { value: 'sales/customer-history', label: 'Customer Purchase History' },
    { value: 'sales/gst', label: 'GST Report' },
    { value: 'sales/old-gold-exchange', label: 'Old Gold Exchange' },
    { value: 'inventory/current-stock', label: 'Current Stock' },
    { value: 'inventory/stock-movement', label: 'Stock Movement' },
    { value: 'inventory/valuation', label: 'Stock Valuation' },
    { value: 'inventory/slow-moving', label: 'Slow-Moving Items' },
    { value: 'loans/active', label: 'Active Loans' },
    { value: 'loans/repayment-history', label: 'Loan Repayment History' },
    { value: 'loans/outstanding', label: 'Outstanding Balance' },
    { value: 'loans/defaulted', label: 'Defaulted Loans' },
    { value: 'loans/interest-income', label: 'Interest Income' },
    { value: 'loans/customer-history', label: 'Customer Loan History' },
    { value: 'loans/pledged-gold', label: 'Pledged Gold' },
    { value: 'financial/profit-loss', label: 'Profit & Loss' },
    { value: 'financial/cash-flow', label: 'Cash Flow' }
  ];

  readonly form = this.fb.nonNullable.group({
    endpoint: ['sales/summary'],
    dateFrom: [this.getDefaultFromDate()],
    dateTo: [this.getDefaultToDate()],
    customerId: ['']
  });

  constructor() {}

  generate(): void {
    this.isLoading = true;
    this.errorMessage = '';
    const value = this.form.getRawValue();
    this.reportType = value.endpoint === 'sales/products' ? 'sales/product-sales' : value.endpoint;

    const params: Record<string, string | number | boolean | null | undefined> = {
      dateFrom: value.dateFrom,
      dateTo: value.dateTo
    };

    if (value.customerId) {
      params['customerId'] = value.customerId;
    }

    this.settings
      .getReport(value.endpoint, params)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.reportData = data;
          this.isLoading = false;
          this.toasts.success('Report', 'Report generated successfully');
        },
        error: (err) => {
          this.isLoading = false;
          this.errorMessage = err.message || 'Failed to generate report';
          this.toasts.error('Error', this.errorMessage);
        }
      });
  }

  exportReport(format: 'json' | 'xlsx' | 'pdf'): void {
    if (!this.reportData) return;

    if (format === 'json') {
      const dataStr = JSON.stringify(this.reportData, null, 2);
      const link = document.createElement('a');
      link.href = 'data:application/json;charset=utf-8,' + encodeURIComponent(dataStr);
      link.download = `report-${this.reportType.replace('/', '-')}-${new Date().toISOString().slice(0, 10)}.json`;
      link.click();
    }
  }

  private getDefaultFromDate(): string {
    const date = new Date();
    date.setDate(1);
    return date.toISOString().slice(0, 10);
  }

  private getDefaultToDate(): string {
    return new Date().toISOString().slice(0, 10);
  }
}
