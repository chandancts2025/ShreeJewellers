import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { CatalogService } from '../../../core/services/catalog';
import { ToastService } from '../../../core/services/toast';
import { Category, InventoryTransaction, InventoryValuation, ProductSummary, StockLevel } from '../../../core/models';

type InventoryPanel = 'catalog' | 'stock' | 'ledger';

@Component({
  selector: 'app-inventory-page',
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './inventory-page.html',
  styleUrl: './inventory-page.scss'
})
export class InventoryPage {
  private readonly catalog = inject(CatalogService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toast = inject(ToastService);

  products: ProductSummary[] = [];
  transactions: InventoryTransaction[] = [];
  lowStock: ProductSummary[] = [];
  categories: Category[] = [];
  stockLevels: StockLevel[] = [];
  valuation: InventoryValuation | null = null;

  activePanel: InventoryPanel = 'catalog';
  currentPage = 1;
  pageSize = 10;
  totalProducts = 0;
  searchTerm = '';
  selectedCategory = '';
  selectedProduct: ProductSummary | null = null;
  qrCodeBase64 = '';
  imagePreviewUrl = '';
  imageFileName = '';

  isLoadingProducts = false;
  isLoadingInventory = false;
  isSavingProduct = false;
  isRecordingStockIn = false;
  isRecordingAdjustment = false;

  readonly pageSizeOptions = [10, 20, 50];
  readonly transactionTypes = ['Purchase', 'Sale', 'Return', 'GoldLoanIn', 'GoldLoanOut', 'StockAdjustment'];
  readonly reasonOptions = [
    { value: 'DAMAGE', label: 'Damage' },
    { value: 'LOST', label: 'Lost' },
    { value: 'COUNT_CORRECTION', label: 'Count Correction' },
    { value: 'RETURN_TO_SUPPLIER', label: 'Return To Supplier' },
    { value: 'DISPLAY_SAMPLE', label: 'Display Sample' },
    { value: 'MELTED', label: 'Melted' }
  ];
  readonly makingChargeTypes = [
    { value: 0, label: 'Per gram' },
    { value: 1, label: 'Percentage' },
    { value: 2, label: 'Fixed amount' }
  ];
  readonly transactionFilters = {
    from: '',
    to: '',
    type: ''
  };

  readonly productForm = this.fb.nonNullable.group({
    id: [0],
    categoryId: [0, [Validators.required, Validators.min(1)]],
    name: ['', Validators.required],
    description: [''],
    skuCode: ['', Validators.required],
    hsnCode: ['7113', Validators.required],
    purity: ['22K', Validators.required],
    netWeightGrams: [0, [Validators.required, Validators.min(0.001)]],
    grossWeightGrams: [0, [Validators.required, Validators.min(0)]],
    stoneWeightGrams: [0, [Validators.required, Validators.min(0)]],
    hallmarkNumber: [''],
    makingChargesType: [0, Validators.required],
    makingChargesValue: [0, [Validators.required, Validators.min(0)]],
    wastagePercent: [0, [Validators.required, Validators.min(0), Validators.max(20)]],
    hallmarkCharges: [0, [Validators.required, Validators.min(0)]],
    gstRatePercent: [3, [Validators.required, Validators.min(0), Validators.max(28)]],
    stoneValue: [0, [Validators.required, Validators.min(0)]],
    stockQuantity: [0, [Validators.required, Validators.min(0)]],
    reorderLevel: [1, [Validators.required, Validators.min(0)]],
    isActive: [true],
    imageBase64: [''],
    imageExtension: ['']
  });

  readonly stockInForm = this.fb.nonNullable.group({
    productId: [0, [Validators.required, Validators.min(1)]],
    quantity: [1, [Validators.required, Validators.min(1)]],
    weightGrams: [0, [Validators.required, Validators.min(0.001)]],
    ratePerGram: [0, [Validators.required, Validators.min(0.01)]],
    supplierName: ['', Validators.required],
    invoiceNumber: [''],
    purchaseDate: [new Date().toISOString().slice(0, 10), Validators.required],
    notes: ['']
  });

  readonly adjustmentForm = this.fb.nonNullable.group({
    productId: [0, [Validators.required, Validators.min(1)]],
    quantityChange: [0, [Validators.required]],
    weightGramsChange: [0, [Validators.required]],
    reasonCode: ['', Validators.required],
    notes: ['', Validators.required]
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loadProducts();
    this.loadCategories();
    this.refreshInventoryData();
  }

  refreshInventoryData(): void {
    this.isLoadingInventory = true;
    this.loadLowStock();
    this.loadStockLevels();
    this.loadValuation();
    this.loadTransactions();
  }

  loadProducts(): void {
    this.isLoadingProducts = true;
    const params: Record<string, string | number | boolean | null | undefined> = {
      page: this.currentPage,
      pageSize: this.pageSize,
      query: this.searchTerm.trim() || undefined,
      categoryId: this.selectedCategory || undefined
    };

    this.catalog.getProducts(params).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isLoadingProducts = false)
    ).subscribe({
      next: (response) => {
        this.products = response.data ?? [];
        this.totalProducts = response.total ?? 0;
      },
      error: () => this.toast.error('Load Products', 'Failed to load products.')
    });
  }

  loadTransactions(): void {
    const params: Record<string, string | number | boolean | null | undefined> = {
      page: 1,
      pageSize: 30,
      from: this.transactionFilters.from || undefined,
      to: this.transactionFilters.to || undefined,
      type: this.transactionFilters.type || undefined
    };

    this.catalog.getTransactions(params).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isLoadingInventory = false)
    ).subscribe({
      next: (response) => this.transactions = response ?? [],
      error: () => this.toast.error('Load Transactions', 'Failed to load inventory transactions.')
    });
  }

  loadLowStock(): void {
    this.catalog.getLowStock().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => this.lowStock = response ?? [],
      error: () => this.toast.error('Load Low Stock', 'Failed to load low stock items.')
    });
  }

  loadCategories(): void {
    this.catalog.getCategories().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => this.categories = response ?? [],
      error: () => this.toast.error('Load Categories', 'Failed to load product categories.')
    });
  }

  loadStockLevels(): void {
    this.catalog.getStockLevels().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => this.stockLevels = response ?? [],
      error: () => this.toast.error('Load Stock Levels', 'Failed to load stock levels.')
    });
  }

  loadValuation(): void {
    this.catalog.getValuation().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => this.valuation = response ?? null,
      error: () => this.toast.error('Load Valuation', 'Failed to load valuation.')
    });
  }

  saveProduct(): void {
    if (this.productForm.invalid) {
      this.productForm.markAllAsTouched();
      this.toast.error('Validation Error', 'Please fix the highlighted product fields.');
      return;
    }

    this.isSavingProduct = true;
    const value = this.productForm.getRawValue();
    const payload = {
      ...value,
      name: value.name.trim(),
      skuCode: value.skuCode.trim().toUpperCase(),
      hsnCode: value.hsnCode.trim(),
      purity: value.purity.trim(),
      description: value.description.trim() || null,
      hallmarkNumber: value.hallmarkNumber.trim() || null
    };

    const request = value.id
      ? this.catalog.updateProduct(value.id, payload)
      : this.catalog.createProduct(payload);

    request.pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isSavingProduct = false)
    ).subscribe({
      next: () => {
        this.toast.success('Success', value.id ? 'Product updated successfully.' : 'Product created successfully.');
        this.resetProductForm();
        this.loadProducts();
        this.refreshInventoryData();
      },
      error: () => this.toast.error('Save Product', 'Failed to save product.')
    });
  }

  deleteProduct(id: number): void {
    if (!confirm('Deactivate this product? It will no longer be available for new stock or sales.')) {
      return;
    }

    this.catalog.deleteProduct(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Success', 'Product deactivated successfully.');
        this.loadProducts();
        this.refreshInventoryData();
      },
      error: () => this.toast.error('Delete Product', 'Failed to deactivate product.')
    });
  }

  editProduct(product: ProductSummary): void {
    this.catalog.getProduct(product.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (detail) => {
        this.clearImage(false);
        this.activePanel = 'catalog';
        this.productForm.patchValue({
          ...detail,
          description: detail.description ?? '',
          hallmarkNumber: detail.hallmarkNumber ?? '',
          makingChargesType: this.toMakingChargeTypeValue(detail.makingChargesType),
          imageBase64: '',
          imageExtension: ''
        });
        window.scrollTo({ top: 0, behavior: 'smooth' });
      },
      error: () => this.toast.error('Product Details', 'Failed to load product details.')
    });
  }

  resetProductForm(): void {
    this.productForm.reset({
      id: 0,
      categoryId: 0,
      name: '',
      description: '',
      skuCode: '',
      hsnCode: '7113',
      purity: '22K',
      netWeightGrams: 0,
      grossWeightGrams: 0,
      stoneWeightGrams: 0,
      hallmarkNumber: '',
      makingChargesType: 0,
      makingChargesValue: 0,
      wastagePercent: 0,
      hallmarkCharges: 0,
      gstRatePercent: 3,
      stoneValue: 0,
      stockQuantity: 0,
      reorderLevel: 1,
      isActive: true,
      imageBase64: '',
      imageExtension: ''
    });
    this.clearImage(false);
  }

  recordStockIn(): void {
    if (this.stockInForm.invalid) {
      this.stockInForm.markAllAsTouched();
      this.toast.error('Validation Error', 'Please fix the highlighted stock-in fields.');
      return;
    }

    this.isRecordingStockIn = true;
    const value = this.stockInForm.getRawValue();
    this.catalog.createStockIn({
      ...value,
      supplierName: value.supplierName.trim(),
      invoiceNumber: value.invoiceNumber.trim() || null,
      notes: value.notes.trim() || null
    }).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isRecordingStockIn = false)
    ).subscribe({
      next: () => {
        this.toast.success('Success', 'Stock-in recorded successfully.');
        this.stockInForm.reset({
          productId: 0,
          quantity: 1,
          weightGrams: 0,
          ratePerGram: 0,
          supplierName: '',
          invoiceNumber: '',
          purchaseDate: new Date().toISOString().slice(0, 10),
          notes: ''
        });
        this.loadProducts();
        this.refreshInventoryData();
      },
      error: () => this.toast.error('Stock In', 'Failed to record stock-in.')
    });
  }

  recordAdjustment(): void {
    if (this.adjustmentForm.invalid) {
      this.adjustmentForm.markAllAsTouched();
      this.toast.error('Validation Error', 'Please fix the highlighted adjustment fields.');
      return;
    }

    const value = this.adjustmentForm.getRawValue();
    if (value.quantityChange === 0) {
      this.toast.error('Adjustment', 'Quantity change cannot be zero.');
      return;
    }

    this.isRecordingAdjustment = true;
    this.catalog.createAdjustment({
      ...value,
      notes: value.notes.trim()
    }).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isRecordingAdjustment = false)
    ).subscribe({
      next: () => {
        this.toast.success('Success', 'Adjustment recorded successfully.');
        this.adjustmentForm.reset({
          productId: 0,
          quantityChange: 0,
          weightGramsChange: 0,
          reasonCode: '',
          notes: ''
        });
        this.loadProducts();
        this.refreshInventoryData();
      },
      error: () => this.toast.error('Adjustment', 'Failed to record adjustment.')
    });
  }

  generateQrCode(productId?: number): void {
    if (!productId) {
      this.qrCodeBase64 = '';
      return;
    }

    this.catalog.getProductQrCode(productId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => {
        this.qrCodeBase64 = response.base64;
        this.selectedProduct = this.products.find(p => p.id === productId) ?? null;
        this.activePanel = 'ledger';
      },
      error: () => this.toast.error('QR Code', 'Failed to generate QR code.')
    });
  }

  onSearch(): void {
    this.currentPage = 1;
    this.loadProducts();
  }

  onPageSizeChange(): void {
    this.currentPage = 1;
    this.loadProducts();
  }

  onPageChange(page: number): void {
    if (page < 1 || page > this.totalPages || page === this.currentPage) {
      return;
    }
    this.currentPage = page;
    this.loadProducts();
  }

  onSkuInput(): void {
    const sku = this.productForm.controls.skuCode.value.toUpperCase().replace(/[^A-Z0-9-]/g, '');
    this.productForm.controls.skuCode.setValue(sku, { emitEvent: false });
  }

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    const extension = file.name.split('.').pop()?.toLowerCase() ?? '';
    if (!['jpg', 'jpeg', 'png'].includes(extension)) {
      this.toast.error('Product Image', 'Please select a JPG or PNG image.');
      input.value = '';
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result ?? '');
      this.imagePreviewUrl = result;
      this.imageFileName = file.name;
      this.productForm.patchValue({
        imageBase64: result.split(',')[1] ?? '',
        imageExtension: extension
      });
    };
    reader.readAsDataURL(file);
  }

  clearImage(showToast = true): void {
    this.imagePreviewUrl = '';
    this.imageFileName = '';
    this.productForm.patchValue({ imageBase64: '', imageExtension: '' });
    if (showToast) {
      this.toast.warning('Product Image', 'Selected image removed.');
    }
  }

  isInvalid(form: FormGroup, controlName: string): boolean {
    const control = form.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  statusClass(stock: StockLevel): string {
    if (stock.currentStock <= stock.reorderLevel) {
      return 'status-danger';
    }
    if (stock.currentStock <= stock.reorderLevel * 1.5) {
      return 'status-pending';
    }
    return 'status-active';
  }

  stockStatus(stock: StockLevel): string {
    if (stock.currentStock <= stock.reorderLevel) {
      return 'Low Stock';
    }
    if (stock.currentStock <= stock.reorderLevel * 1.5) {
      return 'Reorder Soon';
    }
    return 'Healthy';
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalProducts / this.pageSize));
  }

  get pages(): number[] {
    const pages = [];
    for (let i = 1; i <= this.totalPages; i++) {
      pages.push(i);
    }
    return pages;
  }

  get selectedProductName(): string {
    return this.selectedProduct ? `${this.selectedProduct.name} (${this.selectedProduct.skuCode})` : 'Select product';
  }

  private toMakingChargeTypeValue(value: string): number {
    if (value === 'Percentage') {
      return 1;
    }
    if (value === 'Fixed') {
      return 2;
    }
    return 0;
  }
}
