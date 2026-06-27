import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api';
import { Category, InventoryTransaction, InventoryValuation, PagedResponse, Product, ProductSummary, StockLevel } from '../models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly api = inject(ApiService);

  getProducts(params?: Record<string, string | number | boolean | null | undefined>): Observable<PagedResponse<ProductSummary>> {
    return this.api.get<PagedResponse<ProductSummary>>('/api/products', params);
  }

  getCategories(): Observable<Category[]> {
    return this.api.get<Category[]>('/api/products/categories');
  }

  getLowStock(): Observable<ProductSummary[]> {
    return this.api.get<ProductSummary[]>('/api/products/low-stock');
  }

  getProduct(id: number): Observable<Product> {
    return this.api.get<Product>(`/api/products/${id}`);
  }

  getProductBySku(sku: string): Observable<Product> {
    return this.api.get<Product>(`/api/products/by-sku/${encodeURIComponent(sku)}`);
  }

  deleteProduct(id: number): Observable<any> {
    return this.api.delete(`/api/products/${id}`);
  }

  getProductQrCode(id: number): Observable<any> {
    return this.api.get(`/api/products/${id}/qrcode`);
  }

  getStockLevels(): Observable<StockLevel[]> {
    return this.api.get<StockLevel[]>('/api/inventory/stock-levels');
  }

  getTransactions(params?: Record<string, string | number | boolean | null | undefined>): Observable<InventoryTransaction[]> {
    return this.api.get<InventoryTransaction[]>('/api/inventory/transactions', params);
  }

  getValuation(): Observable<InventoryValuation> {
    return this.api.get<InventoryValuation>('/api/inventory/valuation');
  }

  createProduct(payload: unknown): Observable<Product> {
    return this.api.post<Product>('/api/products', payload);
  }

  updateProduct(id: number, payload: unknown): Observable<Product> {
    return this.api.put<Product>(`/api/products/${id}`, payload);
  }

  createStockIn(payload: unknown): Observable<any> {
    return this.api.post('/api/inventory/stock-in', payload);
  }

  createAdjustment(payload: unknown): Observable<any> {
    return this.api.post('/api/inventory/stock-adjustment', payload);
  }

  setManualPriceOverride(payload: unknown): Observable<any> {
    return this.api.post('/api/prices/manual-override', payload);
  }

  getPriceHistory(from: string, to: string): Observable<any> {
    return this.api.get('/api/prices/history', { from, to });
  }
}
