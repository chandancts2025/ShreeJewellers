import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, FormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToastService } from '../../../core/services/toast';
import { SalesService } from '../../../core/services/sales';
import { CatalogService } from '../../../core/services/catalog';
import { AdminService } from '../../../core/services/admin';
import { PublicDataService } from '../../../core/services/public-data';
import { CreateSalesOrderDto, UpdateSalesOrderDto, SalesOrderItem, CreateSalesOrderItemDto, Product, GoldPrice } from '../../../core/models';

@Component({
  selector: 'app-create-sales-order-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterModule],
  templateUrl: './create-sales-order-page.html',
  styleUrls: ['./create-sales-order-page.scss']
})
export class CreateSalesOrderPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly salesService = inject(SalesService);
  private readonly catalogService = inject(CatalogService);
  private readonly adminService = inject(AdminService);
  private readonly publicData = inject(PublicDataService);
  private readonly toastService = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // Expose Math to template
  protected Math = Math;

  orderForm!: FormGroup;
  products: Product[] = [];
  filteredProducts: Product[] = [];
  customers: any[] = [];
  orderItems: (SalesOrderItem & { _tempId?: string })[] = [];
  currentRates: GoldPrice | null = null;
  barcodeScanQuery = '';
  isScanning = false;
  
  isLoading = false;
  showProductSearch = false;
  searchQuery = '';
  selectedProductIdx = -1;

  // Edit mode properties
  isEditMode = false;
  orderId = 0;
  orderNumber = '';
  currentOrderStatus = 'Draft';
  currentPaymentStatus = 'Pending';
  existingInvoiceUrl = '';

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
    this.loadRates();

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.orderId = +idParam;
      this.loadOrderForEdit(this.orderId);
    }
  }

  loadRates(): void {
    this.publicData.getPrices().subscribe({
      next: (prices) => {
        this.currentRates = prices;
      },
      error: () => console.warn('Could not load live rates.')
    });
  }

  initializeForm(): void {
    this.orderForm = this.fb.group({
      customerType: ['registered', Validators.required], // 'registered' or 'walkIn'
      customerUserId: ['', Validators.required],
      walkInCustomerName: [{ value: '', disabled: true }, Validators.required],
      walkInCustomerPhone: [{ value: '', disabled: true }, [Validators.required, Validators.pattern(/^[6-9]\d{9}$/)]],
      orderDate: [new Date().toISOString().split('T')[0], Validators.required],
      paymentMode: ['Cash', Validators.required],
      paymentStatus: ['Paid'],
      status: ['Confirmed'],
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
    this.catalogService.getProducts({ pageSize: 100 }).subscribe({
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
    this.adminService.getCustomers({ pageSize: 100 }).subscribe({
      next: (response: any) => {
        this.customers = Array.isArray(response)
          ? response
          : (response.data || response.items || []);
      },
      error: () => this.toastService.error('Error', 'Failed to load customers')
    });
  }

  loadOrderForEdit(id: number): void {
    this.isLoading = true;
    this.salesService.getOrder(id).subscribe({
      next: (order) => {
        this.isLoading = false;
        this.orderNumber = order.orderNumber;
        this.currentOrderStatus = order.status;
        this.currentPaymentStatus = order.paymentStatus;
        this.existingInvoiceUrl = order.invoiceUrl || '';

        const isRegistered = !!order.customerUserId;
        this.orderForm.patchValue({
          customerType: isRegistered ? 'registered' : 'walkIn',
          customerUserId: order.customerUserId || '',
          walkInCustomerName: order.walkInCustomerName || '',
          walkInCustomerPhone: order.walkInCustomerPhone || '',
          orderDate: order.orderDate ? (order.orderDate.toString().split('T')[0]) : new Date().toISOString().split('T')[0],
          paymentMode: order.paymentMode || 'Cash',
          paymentStatus: order.paymentStatus || 'Paid',
          status: order.status || 'Confirmed',
          amountPaid: order.amountPaid ?? 0,
          advanceAmount: order.advanceAmount ?? 0,
          discountAmount: order.discountAmount ?? 0,
          oldGoldExchangeValue: order.oldGoldExchangeValue ?? 0,
          isInterState: (order.igstAmount || 0) > 0,
          notes: order.notes || ''
        });

        if (isRegistered) {
          this.orderForm.get('customerUserId')?.enable();
          this.orderForm.get('walkInCustomerName')?.disable();
          this.orderForm.get('walkInCustomerPhone')?.disable();
        } else {
          this.orderForm.get('customerUserId')?.disable();
          this.orderForm.get('walkInCustomerName')?.enable();
          this.orderForm.get('walkInCustomerPhone')?.enable();
        }

        if (order.items && order.items.length > 0) {
          this.orderItems = order.items.map((item, idx) => ({
            id: item.id,
            productId: item.productId,
            productName: item.productName,
            skuCode: item.skuCode,
            purity: item.purity,
            quantity: item.quantity,
            weightGrams: item.weightGrams,
            ratePerGram: item.ratePerGram,
            makingCharges: item.makingCharges,
            hallmarkCharges: item.hallmarkCharges,
            stoneValue: item.stoneValue,
            taxPercent: item.taxPercent,
            taxAmount: item.taxAmount,
            discountAmount: item.discountAmount,
            lineTotal: item.lineTotal,
            _tempId: `item-${item.id || idx}`
          }));
        }

        this.calculateTotals();
        this.toastService.info('Order Loaded', `Loaded Sales Order #${this.orderNumber} for editing.`);
      },
      error: (err) => {
        this.isLoading = false;
        this.toastService.error('Error', err.error?.message || `Failed to load order #${id}`);
        this.router.navigate(['/admin/sales/list']);
      }
    });
  }

  // Product search
  onProductSearch(query: string): void {
    this.searchQuery = query.toLowerCase();
    this.filteredProducts = this.products.filter(p => 
      p.name.toLowerCase().includes(this.searchQuery) ||
      p.skuCode.toLowerCase().includes(this.searchQuery) ||
      p.categoryName?.toLowerCase().includes(this.searchQuery) ||
      p.purity.toLowerCase().includes(this.searchQuery)
    );
  }

  getRateForProduct(purity: string, categoryName?: string): number {
    if (!this.currentRates) return 6600;
    const p = (purity || '').toUpperCase();
    const c = (categoryName || '').toUpperCase();

    if (c.includes('SILVER') || p.includes('SILVER') || p.includes('925')) {
      return this.currentRates.silverRatePerGram || 85;
    }
    if (p.includes('24')) {
      return this.currentRates.rate24KPerGram || 7200;
    }
    if (p.includes('22')) {
      return this.currentRates.rate22KPerGram || 6600;
    }
    if (p.includes('18')) {
      return Math.round(this.currentRates.rate24KPerGram * 0.75) || 5400;
    }
    return this.currentRates.rate22KPerGram || 6600;
  }

  scanBarcodeOrSku(): void {
    const raw = (this.barcodeScanQuery || '').trim();
    if (!raw) return;
    const query = raw.toLowerCase();

    // 1. Exact match by SKU or Barcode in local catalog
    const exact = this.products.find(p => 
      p.skuCode.toLowerCase() === query ||
      p.barcodeData === raw
    );

    if (exact) {
      this.selectProduct(exact);
      this.barcodeScanQuery = '';
      return;
    }

    // 2. Keyword / Category / Purity search across local catalog (e.g. "gold", "necklace", "choker")
    const keywordMatches = this.products.filter(p =>
      p.name.toLowerCase().includes(query) ||
      p.skuCode.toLowerCase().includes(query) ||
      p.categoryName?.toLowerCase().includes(query) ||
      p.purity.toLowerCase().includes(query)
    );

    if (keywordMatches.length === 1) {
      this.selectProduct(keywordMatches[0]);
      this.barcodeScanQuery = '';
      return;
    } else if (keywordMatches.length > 1) {
      this.filteredProducts = keywordMatches;
      this.showProductSearch = true;
      this.searchQuery = raw;
      this.toastService.info('Matching Products', `Found ${keywordMatches.length} items for "${raw}". Click any item to add.`);
      return;
    }

    // 3. Fallback to server search
    this.isScanning = true;
    this.catalogService.getProductBySku(raw).subscribe({
      next: (product) => {
        this.isScanning = false;
        this.selectProduct(product);
        this.barcodeScanQuery = '';
      },
      error: () => {
        // Query products API with search filter
        this.catalogService.getProducts({ query: raw, pageSize: 20 }).subscribe({
          next: (res: any) => {
            this.isScanning = false;
            const items: Product[] = Array.isArray(res) ? res : (res.data || res.items || []);
            if (items.length === 1) {
              this.selectProduct(items[0]);
              this.barcodeScanQuery = '';
            } else if (items.length > 1) {
              this.filteredProducts = items;
              this.showProductSearch = true;
              this.searchQuery = raw;
              this.toastService.info('Matching Products', `Found ${items.length} items for "${raw}". Click any item to add.`);
            } else {
              this.toastService.error('Not Found', `No jewellery product found matching SKU, Barcode, or Name "${raw}".`);
            }
          },
          error: () => {
            this.isScanning = false;
            this.toastService.error('Not Found', `No jewellery product found matching SKU/Barcode "${raw}".`);
          }
        });
      }
    });
  }

  selectProduct(product: Product): void {
    this.selectedProductIdx = this.orderItems.length;
    const rate = this.getRateForProduct(product.purity, product.categoryName);
    const weight = product.weight ?? product.netWeightGrams ?? 1;

    let makingCharges = product.makingChargesValue ?? 500;
    if (product.makingChargesType === 'PerGram') {
      makingCharges = Math.round((product.makingChargesValue || 0) * weight);
    } else if (product.makingChargesType === 'FixedPercentage') {
      makingCharges = Math.round((weight * rate) * ((product.makingChargesValue || 0) / 100));
    }

    const hallmarkCharges = product.hallmarkCharges ?? 45;
    const stoneValue = product.stoneValue ?? 0;
    const taxPercent = product.gstRatePercent ?? 3;

    this.orderItems.push({
      id: 0,
      productId: product.id,
      productName: product.name,
      skuCode: product.skuCode,
      purity: product.purity,
      quantity: 1,
      weightGrams: weight,
      ratePerGram: rate,
      makingCharges: makingCharges,
      hallmarkCharges: hallmarkCharges,
      stoneValue: stoneValue,
      taxPercent: taxPercent,
      taxAmount: 0,
      discountAmount: 0,
      lineTotal: 0,
      _tempId: `temp-${Date.now()}`
    });
    this.showProductSearch = false;
    this.searchQuery = '';
    this.calculateLineTotal(this.orderItems.length - 1);
    this.calculateTotals();
    this.toastService.success('Item Added', `${product.name} added at ₹${rate}/g`);
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
    this.netAmount = this.subtotalBeforeTax + this.totalTax - (this.orderForm.get('oldGoldExchangeValue')?.value || 0);
    
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

    if (this.isEditMode) {
      const payload: UpdateSalesOrderDto = {
        id: this.orderId,
        customerUserId: formValue.customerType === 'registered' ? formValue.customerUserId : null,
        walkInCustomerName: formValue.customerType === 'walkIn' ? formValue.walkInCustomerName : undefined,
        walkInCustomerPhone: formValue.customerType === 'walkIn' ? formValue.walkInCustomerPhone : undefined,
        orderDate: formValue.orderDate,
        paymentMode: formValue.paymentMode,
        paymentStatus: formValue.paymentStatus || 'Paid',
        status: formValue.status || 'Confirmed',
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

      this.salesService.updateOrder(this.orderId, payload).subscribe({
        next: (response) => {
          this.isLoading = false;
          this.toastService.success('Success', `Order ${response.orderNumber} updated successfully`);
          this.router.navigate(['/admin/sales/view', this.orderId]);
        },
        error: (err) => {
          this.isLoading = false;
          this.toastService.error('Error', err.error?.message || 'Failed to update order');
        }
      });
    } else {
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
}
