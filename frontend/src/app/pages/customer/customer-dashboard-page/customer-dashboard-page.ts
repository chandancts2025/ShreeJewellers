import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CustomerPortalService } from '../../../core/services/customer-portal';

@Component({
  selector: 'app-customer-dashboard-page',
  imports: [CommonModule, RouterLink],
  templateUrl: './customer-dashboard-page.html',
  styleUrl: './customer-dashboard-page.scss'
})
export class CustomerDashboardPage {
  private readonly portal = inject(CustomerPortalService);
  private readonly destroyRef = inject(DestroyRef);
  data: any;

  constructor() {
    this.portal.getDashboard().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.data = data;
    });
  }
}
