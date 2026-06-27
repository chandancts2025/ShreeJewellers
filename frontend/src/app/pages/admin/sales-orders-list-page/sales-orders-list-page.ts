import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SalesService } from '../../../core/services/sales';
import { ToastService } from '../../../core/services/toast';
import { SalesOrderSummary } from '../../../core/models';

@Component({
  selector: 'app-sales-orders-list-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './sales-orders-list-page.html',
  styleUrls: ['./sales-orders-list-page.scss']
})
export class SalesOrdersListPage implements OnInit {
  private readonly salesService = inject(SalesService);
  private readonly toastService = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  orders: SalesOrderSummary[] = [];
  isLoading = false;
  
  filterForm!: FormGroup;
  currentPage = 1;
  pageSize = 20;
  totalCount = 0;

  statusColors: Record<string, string> = {
    'Draft': '#6c757d',
    'Confirmed': '#0dcaf0',
    'Delivered': '#198754',
    'Cancelled': '#dc3545',
    'Returned': '#fd7e14'
  };

  paymentStatusColors: Record<string, string> = {
    'Pending': '#6c757d',
    'PartiallyPaid': '#ffc107',
    'Paid': '#198754',
    'Refunded': '#dc3545'
  };

  ngOnInit(): void {
    this.initializeFilter();
    this.loadOrders();
  }

  initializeFilter(): void {
    this.filterForm = this.fb.group({
      fromDate: [''],
      toDate: [''],
      customerSearch: [''],
      status: [''],
      paymentStatus: ['']
    });

    // Load filters on change
    this.filterForm.valueChanges.subscribe(() => {
      this.currentPage = 1;
      this.loadOrders();
    });
  }

  loadOrders(): void {
    this.isLoading = true;
    const filters = {
      ...this.filterForm.value,
      page: this.currentPage,
      pageSize: this.pageSize
    };
    
    this.salesService.getOrders(filters).subscribe({
      next: (response) => {
        this.orders = response.data ?? [];
        this.totalCount = response.total ?? this.orders.length;
        this.isLoading = false;
      },
      error: (err) => {
        this.toastService.error('Error', 'Failed to load orders');
        this.isLoading = false;
      }
    });
  }

  getStatusBadgeClass(status: string): string {
    const classMap: Record<string, string> = {
      'Draft': 'badge-secondary',
      'Confirmed': 'badge-info',
      'Delivered': 'badge-success',
      'Cancelled': 'badge-danger',
      'Returned': 'badge-warning'
    };
    return classMap[status] || 'badge-secondary';
  }

  getPaymentStatusBadgeClass(status: string): string {
    const classMap: Record<string, string> = {
      'Pending': 'badge-secondary',
      'PartiallyPaid': 'badge-warning',
      'Paid': 'badge-success',
      'Refunded': 'badge-danger'
    };
    return classMap[status] || 'badge-secondary';
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
      month: 'short',
      day: 'numeric'
    });
  }

  resetFilters(): void {
    this.filterForm.reset();
    this.currentPage = 1;
  }

  previousPage(): void {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.loadOrders();
    }
  }

  nextPage(): void {
    if (this.currentPage * this.pageSize < this.totalCount) {
      this.currentPage++;
      this.loadOrders();
    }
  }

  get totalPages(): number {
    return Math.ceil(this.totalCount / this.pageSize);
  }

  get displayStart(): number {
    return (this.currentPage - 1) * this.pageSize + 1;
  }

  get displayEnd(): number {
    return Math.min(this.currentPage * this.pageSize, this.totalCount);
  }
}
