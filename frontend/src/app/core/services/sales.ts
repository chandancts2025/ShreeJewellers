import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api';
import { CreateSalesOrderDto, UpdateSalesOrderDto, DailySalesSummaryDto, PagedResponse, ReturnOrderDto, SalesFilterDto, SalesOrderResponse, SalesOrderSummary } from '../models';

@Injectable({ providedIn: 'root' })
export class SalesService {
  private readonly api = inject(ApiService);

  getMyOrders(page = 1, pageSize = 10): Observable<PagedResponse<SalesOrderSummary>> {
    return this.api.get<PagedResponse<SalesOrderSummary>>('/api/sales/my-orders', { page, pageSize });
  }

  getOrders(filters?: Partial<SalesFilterDto>): Observable<PagedResponse<SalesOrderSummary>> {
    const params = {
      page: filters?.page ?? 1,
      pageSize: filters?.pageSize ?? 20,
      fromDate: filters?.fromDate,
      toDate: filters?.toDate,
      customerSearch: filters?.customerSearch,
      status: filters?.status,
      paymentStatus: filters?.paymentStatus
    } satisfies Record<string, string | number | boolean | null | undefined>;

    return this.api.get<PagedResponse<SalesOrderSummary>>('/api/sales', params);
  }

  getOrder(id: number): Observable<SalesOrderResponse> {
    return this.api.get<SalesOrderResponse>(`/api/sales/${id}`);
  }

  createOrder(payload: CreateSalesOrderDto): Observable<SalesOrderResponse> {
    return this.api.post<SalesOrderResponse>('/api/sales', payload);
  }

  updateOrder(id: number, payload: UpdateSalesOrderDto): Observable<SalesOrderResponse> {
    return this.api.put<SalesOrderResponse>(`/api/sales/${id}`, payload);
  }

  getInvoice(id: number): Observable<{ invoiceUrl: string }> {
    return this.api.get<{ invoiceUrl: string }>(`/api/sales/${id}/invoice`);
  }

  sendInvoice(id: number, email?: string, phone?: string): Observable<{ success: boolean; message: string }> {
    return this.api.post<{ success: boolean; message: string }>(`/api/sales/${id}/send-invoice`, {}, { email, phone });
  }

  processReturn(id: number, payload: ReturnOrderDto): Observable<SalesOrderResponse> {
    return this.api.post<SalesOrderResponse>(`/api/sales/${id}/return`, payload);
  }

  getDailySummary(date?: string): Observable<DailySalesSummaryDto> {
    return this.api.get<DailySalesSummaryDto>('/api/sales/daily-summary', { date });
  }
}
