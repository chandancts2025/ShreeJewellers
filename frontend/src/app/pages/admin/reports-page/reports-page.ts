import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SettingsService } from '../../../core/services/settings';
import { AdminService } from '../../../core/services/admin';
import { ToastService } from '../../../core/services/toast';
import { CustomerListItem } from '../../../core/models';

@Component({
  selector: 'app-reports-page',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './reports-page.html',
  styleUrl: './reports-page.scss'
})
export class ReportsPage {
  private readonly settings = inject(SettingsService);
  private readonly adminService = inject(AdminService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toasts = inject(ToastService);

  reportData: any = null;
  reportType = '';
  isLoading = false;
  isExporting = false;
  errorMessage = '';
  customers: CustomerListItem[] = [];
  selectedCustomerName = '';

  readonly reportOptions = [
    { value: 'sales/summary', label: 'Sales Summary', requiresCustomer: false },
    { value: 'sales/products', label: 'Product-wise Sales', requiresCustomer: false },
    { value: 'sales/customer-history', label: 'Customer Purchase History', requiresCustomer: true },
    { value: 'sales/gst', label: 'GST Filing Report', requiresCustomer: false },
    { value: 'sales/old-gold-exchange', label: 'Old Gold Exchange', requiresCustomer: false },
    { value: 'inventory/current-stock', label: 'Current Stock & Rates', requiresCustomer: false },
    { value: 'inventory/stock-movement', label: 'Stock Movement Ledger', requiresCustomer: false },
    { value: 'inventory/valuation', label: 'Inventory Valuation', requiresCustomer: false },
    { value: 'inventory/slow-moving', label: 'Slow-Moving Items (90+ Days)', requiresCustomer: false },
    { value: 'loans/active', label: 'Active Gold Loans Summary', requiresCustomer: false },
    { value: 'loans/repayment-history', label: 'Loan Repayment Ledger', requiresCustomer: false },
    { value: 'loans/outstanding', label: 'Outstanding Balance Ledger', requiresCustomer: false },
    { value: 'loans/defaulted', label: 'Defaulted & At-Risk Loans', requiresCustomer: false },
    { value: 'loans/interest-income', label: 'Interest & Penalty Income', requiresCustomer: false },
    { value: 'loans/customer-history', label: 'Customer Gold Loan History', requiresCustomer: true },
    { value: 'loans/pledged-gold', label: 'Pledged Gold Collateral Vault', requiresCustomer: false },
    { value: 'financial/profit-loss', label: 'Profit & Loss Statement', requiresCustomer: false },
    { value: 'financial/cash-flow', label: 'Cash Flow Summary', requiresCustomer: false }
  ];

  readonly form = this.fb.nonNullable.group({
    endpoint: ['sales/summary', Validators.required],
    dateFrom: [this.getDefaultFromDate(), Validators.required],
    dateTo: [this.getDefaultToDate(), Validators.required],
    customerId: ['']
  });

  constructor() {
    this.loadCustomers();
    // Auto-select first customer when user switches to customer-specific report
    this.form.controls.endpoint.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((endpoint) => {
        if (this.isCustomerReport(endpoint)) {
          if (!this.form.controls.customerId.value && this.customers.length > 0) {
            this.form.patchValue({ customerId: this.customers[0].userId });
          }
        }
      });
  }

  isCustomerReport(endpoint?: string): boolean {
    const ep = endpoint ?? this.form.controls.endpoint.value;
    return ep === 'sales/customer-history' || ep === 'loans/customer-history';
  }

  private loadCustomers(): void {
    this.adminService.getCustomers({ pageSize: 100 })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res: any) => {
          this.customers = res?.items ?? res?.data ?? [];
        },
        error: () => {
          // Soft fail
        }
      });
  }

  setPreset(preset: 'today' | 'this-month' | 'last-30' | 'this-fy' | 'all-time'): void {
    const now = new Date();
    const todayStr = now.toISOString().slice(0, 10);

    if (preset === 'today') {
      this.form.patchValue({ dateFrom: todayStr, dateTo: todayStr });
    } else if (preset === 'this-month') {
      const firstDay = new Date(now.getFullYear(), now.getMonth(), 1).toISOString().slice(0, 10);
      this.form.patchValue({ dateFrom: firstDay, dateTo: todayStr });
    } else if (preset === 'last-30') {
      const past30 = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
      this.form.patchValue({ dateFrom: past30, dateTo: todayStr });
    } else if (preset === 'this-fy') {
      // Indian Financial Year: April 1 to March 31
      const year = now.getMonth() >= 3 ? now.getFullYear() : now.getFullYear() - 1;
      const fyStart = `${year}-04-01`;
      this.form.patchValue({ dateFrom: fyStart, dateTo: todayStr });
    } else if (preset === 'all-time') {
      this.form.patchValue({ dateFrom: '2020-01-01', dateTo: todayStr });
    }
  }

  generate(): void {
    this.errorMessage = '';
    const value = this.form.getRawValue();

    if (this.isCustomerReport(value.endpoint) && !value.customerId) {
      if (this.customers.length > 0) {
        value.customerId = this.customers[0].userId;
        this.form.patchValue({ customerId: value.customerId });
      } else {
        this.errorMessage = 'Please select a customer to view customer history.';
        this.toasts.error('Customer Required', this.errorMessage);
        return;
      }
    }

    const selectedCust = this.customers.find(c => c.userId === value.customerId);
    this.selectedCustomerName = selectedCust ? `${selectedCust.fullName} (${selectedCust.customerCode || selectedCust.phoneNumber})` : '';

    this.isLoading = true;
    this.reportType = value.endpoint;

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
          this.toasts.success('Report Generated', `Loaded ${this.getReportLabel(value.endpoint)} successfully.`);
        },
        error: (err) => {
          this.isLoading = false;
          this.errorMessage = err.error?.message || err.message || 'Failed to generate report.';
          this.toasts.error('Report Error', this.errorMessage);
        }
      });
  }

  getReportLabel(endpoint: string): string {
    const opt = this.reportOptions.find(o => o.value === endpoint);
    return opt ? opt.label : endpoint;
  }

  exportReport(format: 'json' | 'xlsx' | 'pdf'): void {
    if (!this.reportData) return;

    if (format === 'json') {
      const dataStr = JSON.stringify(this.reportData, null, 2);
      const link = document.createElement('a');
      link.href = 'data:application/json;charset=utf-8,' + encodeURIComponent(dataStr);
      link.download = `report-${this.reportType.replace('/', '-')}-${new Date().toISOString().slice(0, 10)}.json`;
      link.click();
      this.toasts.success('Export', 'JSON report downloaded.');
      return;
    }

    if (format === 'pdf') {
      window.print();
      return;
    }

    if (format === 'xlsx') {
      this.isExporting = true;
      const value = this.form.getRawValue();
      const params: Record<string, string | number | boolean | null | undefined> = {
        dateFrom: value.dateFrom,
        dateTo: value.dateTo,
        exportFormat: 'xlsx'
      };
      if (value.customerId) {
        params['customerId'] = value.customerId;
      }

      this.settings.downloadReport(value.endpoint, params)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (blob) => {
            this.isExporting = false;
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `${this.reportType.replace('/', '_')}_${new Date().toISOString().slice(0, 10)}.xlsx`;
            a.click();
            window.URL.revokeObjectURL(url);
            this.toasts.success('Excel Export', 'Excel spreadsheet downloaded.');
          },
          error: (err) => {
            this.isExporting = false;
            this.toasts.error('Export Error', err.message || 'Failed to download Excel report.');
          }
        });
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
