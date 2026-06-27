import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastService } from '../../../core/services/toast';
import { SalesService } from '../../../core/services/sales';
import { CatalogService } from '../../../core/services/catalog';
import { AdminService } from '../../../core/services/admin';
import { CreateSalesOrderDto, SalesOrderItem, CreateSalesOrderItemDto, Product } from '../../../core/models';

@Component({
  selector: 'app-create-sales-order-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './create-sales-order-page.html',
  styleUrls: ['./create-sales-order-page.scss']
})
export class CreateSalesOrderPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly salesService = inject(SalesService);
  private readonly catalogService = inject(CatalogService);
  private readonly adminService = inject(AdminService);
  private readonly toastService = inject(ToastService);
  private readonly router = inject(Router);

  // Expose Math to template
  protected Math = Math;

  orderForm!: FormGroup;
  products: Product[] = [];
  filteredProducts: Product[] = [];
  customers: any[] = [];
  orderItems: (SalesOrderItem & { _tempId?: string })[] = [];
  
  isLoading = false;
  showProductSearch = false;
  searchQuery = '';
  selectedProductIdx = -1;

  // Financial calculations
  grossAmount = 0;
  discountAmount = 0;
  subtotalBeforeTax = 0;
  totalTax = 0;
  cgstAmount = 0;
  sgstAmount = 0;
  igstAmount = 0;
  netAmount = 0;
  balanceDue = 0;

  ngOnInit(): void {
    this.initializeForm();
    this.loadProducts();
    this.loadCustomers();
  }

  initializeForm(): void {
    this.orderForm = this.fb.group({
      customerType: ['registered', Validators.required], // 'registered' or 'walkIn'
      customerUserId: ['', Validators.required],
      walkInCustomerName: [{ value: '', disabled: true }, Validators.required],
      walkInCustomerPhone: [{ value: '', disabled: true }, [Validators.required, Validators.pattern(/^[6-9]\d{9}$/)]],
      orderDate: [new Date().toISOString().split('T')[0], Validators.required],
      paymentMode: ['Cash', Validators.required],
      amountPaid: [0, [Validators.required, Validators.min(0)]],
      advanceAmount: [0, [Validators.min(0)]],
      discountAmount: [0, [Validators.min(0)]],
      oldGoldExchangeValue: [0, [Validators.min(0)]],
      oldGoldWeightGrams: [0, [Validators.min(0)]],
      oldGoldPurity: ['22K'],
      isInterState: [false],
      notes: ['']
    });

    // Handle customer type toggle
    this.orderForm.get('customerType')?.valueChanges.subscribe(type => {
      if (type === 'registered') {
        this.orderForm.get('customerUserId')?.enable();
        this.orderForm.get('walkInCustomerName')?.disable();
        this.orderForm.get('walkInCustomerPhone')?.disable();
      } else {
        this.orderForm.get('customerUserId')?.disable();
        this.orderForm.get('walkInCustomerName')?.enable();
        this.orderForm.get('walkInCustomerPhone')?.enable();
      }
    });
  }

  loadProducts(): void {
    this.catalogService.getProducts().subscribe({
      next: (response: any) => {
        this.products = Array.isArray(response)
          ? response
          : (response.items || response.data || []);
        this.filteredProducts = this.products;
      },
      error: () => this.toastService.error('Error', 'Failed to load products')
    });
  }

  loadCustomers(): void {
    this.adminService.getCustomers().subscribe({
      next: (response: any) => {
        this.customers = Array.isArray(response)
          ? response
          : (response.data || response.items || []);
      },
      error: () => this.toastService.error('Error', 'Failed to load customers')
    });
  }

  // Product search
  onProductSearch(query: string): void {
    this.searchQuery = query.toLowerCase();
    this.filteredProducts = this.products.filter(p => 
      p.name.toLowerCase().includes(this.searchQuery) ||
      p.skuCode.toLowerCase().includes(this.searchQuery) ||
      p.categoryName?.toLowerCase().includes(this.searchQuery)
    );
  }

  selectProduct(product: Product): void {
    this.selectedProductIdx = this.orderItems.length;
    this.orderItems.push({
      id: 0,
      productId: product.id,
      productName: product.name,
      skuCode: product.skuCode,
      purity: product.purity,
      quantity: 1,
      weightGrams: product.weight ?? product.netWeightGrams,
      ratePerGram: 6500, // Default rate
      makingCharges: 500,
      hallmarkCharges: 200,
      stoneValue: 0,
      taxPercent: 3,
      taxAmount: 0,
      discountAmount: 0,
      lineTotal: 0,
      _tempId: `temp-${Date.now()}`
    });
    this.showProductSearch = false;
    this.searchQuery = '';
    this.calculateTotals();
  }

  updateLineItem(index: number, field: string, value: any): void {
    const item = this.orderItems[index];
    (item as any)[field] = isNaN(value) ? value : parseFloat(value);
    this.calculateLineTotal(index);
    this.calculateTotals();
  }

  calculateLineTotal(index: number): void {
    const item = this.orderItems[index];
    const metalValue = item.weightGrams * item.ratePerGram;
    const subtotal = metalValue + item.makingCharges + item.hallmarkCharges + item.stoneValue - (item.discountAmount || 0);
    item.taxAmount = subtotal * (item.taxPercent / 100);
    item.lineTotal = subtotal + item.taxAmount;
  }

  removeLineItem(index: number): void {
    this.orderItems.splice(index, 1);
    if (this.selectedProductIdx === index) this.selectedProductIdx = -1;
    this.calculateTotals();
  }

  calculateTotals(): void {
    if (this.orderItems.length === 0) {
      this.grossAmount = this.discountAmount = this.subtotalBeforeTax = this.totalTax = 0;
      this.cgstAmount = this.sgstAmount = this.igstAmount = 0;
      this.netAmount = this.balanceDue = 0;
      return;
    }

    this.grossAmount = this.orderItems.reduce((sum, item) => {
      const metalValue = item.weightGrams * item.ratePerGram;
      return sum + metalValue + item.makingCharges + item.hallmarkCharges + item.stoneValue;
    }, 0);

    this.discountAmount = this.orderItems.reduce((sum, item) => sum + (item.discountAmount || 0), 0) + 
                         (this.orderForm.get('discountAmount')?.value || 0);
    this.subtotalBeforeTax = this.grossAmount - this.discountAmount;
    
    const totalTaxableAmount = this.orderItems.reduce((sum, item) => {
      const metalValue = item.weightGrams * item.ratePerGram;
      const itemSubtotal = metalValue + item.makingCharges + item.hallmarkCharges + item.stoneValue - (item.discountAmount || 0);
      return sum + itemSubtotal;
    }, 0);

    const isInterState = this.orderForm.get('isInterState')?.value;
    if (isInterState) {
      this.igstAmount = totalTaxableAmount * 0.03;
      this.cgstAmount = 0;
      this.sgstAmount = 0;
    } else {
      this.cgstAmount = totalTaxableAmount * 0.015;
      this.sgstAmount = totalTaxableAmount * 0.015;
      this.igstAmount = 0;
    }
    
    this.totalTax = this.cgstAmount + this.sgstAmount + this.igstAmount;
    this.netAmount = this.subtotalBeforeTax + this.totalTax + (this.orderForm.get('oldGoldExchangeValue')?.value || 0) * -1;
    
    const amountPaid = this.orderForm.get('amountPaid')?.value || 0;
    this.balanceDue = this.netAmount - amountPaid;
  }

  onAmountPaidChange(): void {
    this.calculateTotals();
  }

  onDiscountChange(): void {
    this.calculateTotals();
  }

  onOldGoldChange(): void {
    this.calculateTotals();
  }

  submitOrder(): void {
    if (this.orderForm.invalid) {
      this.toastService.error('Validation Error', 'Please fill all required fields');
      return;
    }

    if (this.orderItems.length === 0) {
      this.toastService.error('No Items', 'Please add at least one item');
      return;
    }

    this.isLoading = true;
    const formValue = this.orderForm.getRawValue();
    
    const payload: CreateSalesOrderDto = {
      customerUserId: formValue.customerType === 'registered' ? formValue.customerUserId : null,
      walkInCustomerName: formValue.customerType === 'walkIn' ? formValue.walkInCustomerName : undefined,
      walkInCustomerPhone: formValue.customerType === 'walkIn' ? formValue.walkInCustomerPhone : undefined,
      orderDate: formValue.orderDate,
      paymentMode: formValue.paymentMode,
      amountPaid: formValue.amountPaid,
      advanceAmount: formValue.advanceAmount,
      discountAmount: formValue.discountAmount,
      oldGoldExchangeValue: formValue.oldGoldExchangeValue,
      oldGoldWeightGrams: formValue.oldGoldWeightGrams,
      oldGoldPurity: formValue.oldGoldPurity,
      isInterState: formValue.isInterState,
      notes: formValue.notes,
      items: this.orderItems.map(item => ({
        productId: item.productId,
        quantity: item.quantity,
        weightGrams: item.weightGrams,
        ratePerGram: item.ratePerGram,
        makingCharges: item.makingCharges,
        hallmarkCharges: item.hallmarkCharges,
        stoneValue: item.stoneValue,
        discountAmount: item.discountAmount
      }))
    };

    this.salesService.createOrder(payload).subscribe({
      next: (response) => {
        this.isLoading = false;
        this.toastService.success('Success', `Order ${response.orderNumber} created successfully`);
        this.router.navigate(['/admin/sales/view', response.id]);
      },
      error: (err) => {
        this.isLoading = false;
        this.toastService.error('Error', err.error?.message || 'Failed to create order');
      }
    });
  }
}
