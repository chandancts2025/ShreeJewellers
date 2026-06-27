import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SalesService } from '../../../core/services/sales';

@Component({
  selector: 'app-my-orders-page',
  imports: [CommonModule],
  templateUrl: './my-orders-page.html',
  styleUrl: './my-orders-page.scss'
})
export class MyOrdersPage {
  private readonly sales = inject(SalesService);
  private readonly destroyRef = inject(DestroyRef);
  orders: any[] = [];

  constructor() {
    this.sales.getMyOrders().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((response) => {
      this.orders = response.data ?? [];
    });
  }

  downloadInvoice(id: number): void {
    this.sales.getInvoice(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((response) => {
      window.open(response.invoiceUrl, '_blank');
    });
  }
}
