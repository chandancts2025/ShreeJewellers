import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { SalesService } from '../../../core/services/sales';
import { ToastService } from '../../../core/services/toast';
import { SalesOrderResponse } from '../../../core/models';

@Component({
  selector: 'app-order-details-page',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './order-details-page.html',
  styleUrls: ['./order-details-page.scss']
})
export class OrderDetailsPage implements OnInit {
  private readonly salesService = inject(SalesService);
  private readonly toastService = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  // Expose Math to template
  protected Math = Math;

  order: SalesOrderResponse | null = null;
  isLoading = true;
  isSendingInvoice = false;
  showSendInvoiceForm = false;
  invoiceEmail = '';
  invoicePhone = '';

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadOrderDetails(parseInt(id));
    }
  }

  loadOrderDetails(id: number): void {
    this.isLoading = true;
    this.salesService.getOrder(id).subscribe({
      next: (response) => {
        this.order = response;
        this.isLoading = false;
      },
      error: (err) => {
        this.toastService.error('Error', 'Failed to load order details');
        this.isLoading = false;
      }
    });
  }

  getStatusColor(status: string): string {
    const colorMap: Record<string, string> = {
      'Draft': '#6c757d',
      'Confirmed': '#0dcaf0',
      'Delivered': '#198754',
      'Cancelled': '#dc3545',
      'Returned': '#fd7e14'
    };
    return colorMap[status] || '#6c757d';
  }

  getPaymentStatusColor(status: string): string {
    const colorMap: Record<string, string> = {
      'Pending': '#6c757d',
      'PartiallyPaid': '#ffc107',
      'Paid': '#198754',
      'Refunded': '#dc3545'
    };
    return colorMap[status] || '#6c757d';
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      minimumFractionDigits: 2
    }).format(value);
  }

  formatDate(date: string): string {
    return new Date(date).toLocaleDateString('en-IN', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  downloadInvoice(): void {
    if (!this.order) return;
    
    this.salesService.getInvoice(this.order.id).subscribe({
      next: (response: any) => {
        window.open(response.invoiceUrl, '_blank');
        this.toastService.success('Success', 'Invoice opened in new window');
      },
      error: () => {
        this.toastService.error('Error', 'Failed to download invoice');
      }
    });
  }

  sendInvoice(): void {
    if (!this.order) return;
    if (!this.invoiceEmail && !this.invoicePhone) {
      this.toastService.error('Validation Error', 'Please enter email or phone number');
      return;
    }

    this.isSendingInvoice = true;
    this.salesService.sendInvoice(this.order.id, this.invoiceEmail || undefined, this.invoicePhone || undefined)
      .subscribe({
        next: () => {
          this.toastService.success('Success', 'Invoice sent successfully');
          this.showSendInvoiceForm = false;
          this.invoiceEmail = '';
          this.invoicePhone = '';
          this.isSendingInvoice = false;
        },
        error: () => {
          this.toastService.error('Error', 'Failed to send invoice');
          this.isSendingInvoice = false;
        }
      });
  }

  editOrder(): void {
    if (!this.order) return;
    this.router.navigate(['/admin/sales/edit', this.order.id]);
  }

  processReturn(): void {
    if (!this.order) return;
    this.router.navigate(['/admin/sales/return', this.order.id]);
  }

  cancelOrder(): void {
    if (!this.order) return;
    if (confirm('Are you sure you want to cancel this order?')) {
      this.toastService.warning('Info', 'Order cancellation not yet implemented');
    }
  }
}
