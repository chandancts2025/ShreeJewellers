import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SalesService } from '../../../core/services/sales';
import { ToastService } from '../../../core/services/toast';
import { ReturnOrderDto, SalesOrderResponse } from '../../../core/models';

@Component({
  selector: 'app-process-return-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './process-return-page.html',
  styleUrls: ['./process-return-page.scss']
})
export class ProcessReturnPage implements OnInit {
  private readonly salesService = inject(SalesService);
  private readonly toastService = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  // Expose Math to template
  protected Math = Math;

  returnForm!: FormGroup;
  order: SalesOrderResponse | null = null;
  selectedItems: Set<number> = new Set();
  isLoading = false;
  isProcessing = false;

  // Financial calculations
  originalAmount = 0;
  returnAmount = 0;
  refundAmount = 0;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadOrderDetails(parseInt(id));
    }
    this.initializeForm();
  }

  initializeForm(): void {
    this.returnForm = this.fb.group({
      returnReason: ['', [Validators.required, Validators.minLength(10)]],
      refundMode: ['Cash', Validators.required],
      notes: ['']
    });
  }

  loadOrderDetails(id: number): void {
    this.isLoading = true;
    this.salesService.getOrder(id).subscribe({
      next: (response) => {
        this.order = response;
        this.originalAmount = response.netAmount;
        this.isLoading = false;
      },
      error: (err) => {
        this.toastService.error('Error', 'Failed to load order details');
        this.isLoading = false;
      }
    });
  }

  toggleItemSelection(itemId: number): void {
    if (this.selectedItems.has(itemId)) {
      this.selectedItems.delete(itemId);
    } else {
      this.selectedItems.add(itemId);
    }
    this.calculateReturnAmount();
  }

  isItemSelected(itemId: number): boolean {
    return this.selectedItems.has(itemId);
  }

  selectAllItems(): void {
    if (!this.order) return;
    this.selectedItems.clear();
    this.order.items.forEach(item => this.selectedItems.add(item.id));
    this.calculateReturnAmount();
  }

  deselectAllItems(): void {
    this.selectedItems.clear();
    this.calculateReturnAmount();
  }

  calculateReturnAmount(): void {
    if (!this.order) return;

    this.returnAmount = this.order.items.reduce((sum, item) => {
      if (this.selectedItems.has(item.id)) {
        return sum + item.lineTotal;
      }
      return sum;
    }, 0);

    this.refundAmount = this.returnAmount;
  }

  getItemStatus(itemId: number): string {
    return this.selectedItems.has(itemId) ? 'Selected for return' : 'Not selected';
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

  submitReturn(): void {
    if (this.returnForm.invalid) {
      this.toastService.error('Validation Error', 'Please fill all required fields');
      return;
    }

    if (this.selectedItems.size === 0) {
      this.toastService.error('Error', 'Please select at least one item to return');
      return;
    }

    if (!this.order) {
      this.toastService.error('Error', 'Order not found');
      return;
    }

    if (confirm(`Process return for ${this.selectedItems.size} item(s)?\nRefund Amount: ${this.formatCurrency(this.refundAmount)}`)) {
      this.isProcessing = true;
      const payload: ReturnOrderDto = {
        originalOrderId: this.order.id,
        itemIdsToReturn: Array.from(this.selectedItems),
        returnReason: this.returnForm.get('returnReason')?.value,
        refundMode: this.returnForm.get('refundMode')?.value,
        notes: this.returnForm.get('notes')?.value
      };

      this.salesService.processReturn(this.order.id, payload).subscribe({
        next: (response) => {
          this.isProcessing = false;
          this.toastService.success('Success', 'Return processed successfully');
          this.router.navigate(['/admin/sales/view', response.id]);
        },
        error: (err) => {
          this.isProcessing = false;
          this.toastService.error('Error', err.error?.message || 'Failed to process return');
        }
      });
    }
  }

  cancel(): void {
    window.history.back();
  }
}
