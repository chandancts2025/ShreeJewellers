import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminService } from '../../../core/services/admin';

@Component({
  selector: 'app-admin-dashboard-page',
  imports: [CommonModule, RouterLink],
  templateUrl: './admin-dashboard-page.html',
  styleUrl: './admin-dashboard-page.scss'
})
export class AdminDashboardPage {
  private readonly admin = inject(AdminService);
  private readonly destroyRef = inject(DestroyRef);
  data: any;

  constructor() {
    this.admin.getDashboard().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.data = data;
    });
  }

  getBarWidth(amount: number): number {
    if (!this.data?.salesTrend12Months?.length) return 0;
    const max = Math.max(...this.data.salesTrend12Months.map((p: any) => p.amount || 0), 1);
    return Math.min(100, Math.max(5, Math.round((amount / max) * 100)));
  }
}
