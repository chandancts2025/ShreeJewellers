import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SalesService } from '../../../core/services/sales';
import { DailySalesSummaryDto, SalesOrderSummary } from '../../../core/models';
import { ToastService } from '../../../core/services/toast';

@Component({
  selector: 'app-sales-billing-page',
  imports: [CommonModule, RouterLink],
  template: `
    <section class="app-page">
      <div class="container">
        <div class="section-title mb-4">Sales & Billing</div>

        <div class="lux-card p-4 mb-4 sales-hero">
          <div class="row g-4 align-items-center">
            <div class="col-lg-7">
              <div class="font-display fs-2 mb-2">One billing hub for the full sales flow</div>
              <p class="text-secondary mb-0">
                Start an order, review invoices, and track the day’s collections from a single page instead of repeating the same actions across screens.
              </p>
            </div>
            <div class="col-lg-5">
              <div class="d-grid gap-2">
                <a class="btn lux-btn lux-btn-primary" routerLink="/admin/sales/create">Create New Order</a>
                <a class="btn btn-outline-secondary" routerLink="/admin/sales/list">Browse Orders</a>
                <a class="btn btn-outline-secondary" routerLink="/admin/sales/summary">Open Daily Summary</a>
              </div>
            </div>
          </div>
        </div>

        <div class="row g-4 mb-4">
          <div class="col-md-4">
            <div class="lux-card p-4 h-100">
              <div class="small text-secondary text-uppercase">Today</div>
              <div class="font-display fs-3">{{ summary?.orderCount ?? 0 }}</div>
              <div class="text-secondary">orders billed</div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="lux-card p-4 h-100">
              <div class="small text-secondary text-uppercase">Collections</div>
              <div class="font-display fs-3">{{ formatCurrency(summary?.totalAmountReceived ?? 0) }}</div>
              <div class="text-secondary">received today</div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="lux-card p-4 h-100">
              <div class="small text-secondary text-uppercase">Recent Revenue</div>
              <div class="font-display fs-3">{{ formatCurrency(recentRevenue) }}</div>
              <div class="text-secondary">from latest invoices</div>
            </div>
          </div>
        </div>

        <div class="row g-4">
          <div class="col-lg-4">
            <div class="lux-card p-4 h-100">
              <div class="font-display fs-4 mb-3">Payment Mix</div>
              @if (paymentModes.length) {
                @for (mode of paymentModes; track mode[0]) {
                  <div class="d-flex justify-content-between align-items-center py-2 border-bottom">
                    <span>{{ mode[0] }}</span>
                    <strong>{{ formatCurrency(mode[1]) }}</strong>
                  </div>
                }
              } @else {
                <div class="text-secondary">No sales recorded for this day yet.</div>
              }
            </div>
          </div>

          <div class="col-lg-8">
            <div class="lux-card p-4">
              <div class="d-flex justify-content-between align-items-center mb-3">
                <div>
                  <div class="font-display fs-4">Recent Orders</div>
                  <div class="small text-secondary">Live data from the shared sales endpoint</div>
                </div>
                <a class="btn btn-sm btn-outline-secondary" routerLink="/admin/sales/list">View all</a>
              </div>

              <div class="table-responsive">
                <table class="table align-middle">
                  <thead>
                    <tr>
                      <th>Invoice</th>
                      <th>Date</th>
                      <th>Customer</th>
                      <th>Amount</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    @if (!history.length && !isLoading) {
                      <tr>
                        <td colspan="5" class="text-center text-secondary py-4">No orders found.</td>
                      </tr>
                    }
                    @for (order of history; track order.id) {
                      <tr>
                        <td>
                          <a [routerLink]="['/admin/sales/view', order.id]" class="text-decoration-none fw-semibold">
                            {{ order.orderNumber }}
                          </a>
                        </td>
                        <td>{{ formatDate(order.orderDate) }}</td>
                        <td>{{ order.customerName || 'Walk-in customer' }}</td>
                        <td>{{ formatCurrency(order.netAmount) }}</td>
                        <td><span class="badge bg-light text-dark border">{{ order.status }}</span></td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  `,
  styleUrl: './sales-billing-page.scss'
})
export class SalesBillingPage {
  private readonly sales = inject(SalesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toast = inject(ToastService);

  history: SalesOrderSummary[] = [];
  summary: DailySalesSummaryDto | null = null;
  isLoading = false;

  constructor() {
    this.load();
  }

  load(): void {
    this.isLoading = true;

    this.sales.getOrders({ page: 1, pageSize: 8 })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.history = response.data ?? [];
          this.isLoading = false;
        },
        error: () => {
          this.toast.error('Sales History', 'Failed to load recent sales.');
          this.isLoading = false;
        }
      });

    this.sales.getDailySummary()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => this.summary = response,
        error: () => this.toast.error('Sales Summary', 'Failed to load today\'s summary.')
      });
  }

  get paymentModes(): Array<[string, number]> {
    return this.summary ? Object.entries(this.summary.byPaymentMode) : [];
  }

  get recentRevenue(): number {
    return this.history.reduce((sum, order) => sum + order.netAmount, 0);
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0
    }).format(value ?? 0);
  }

  formatDate(value: string): string {
    return new Date(value).toLocaleDateString('en-IN', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }
}
