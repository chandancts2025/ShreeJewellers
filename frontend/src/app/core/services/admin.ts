import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api';
import { AdminCreateUserPayload, CustomerDetail, CustomerListItem, PagedResponse, UpdateCustomerPayload } from '../models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly api = inject(ApiService);

  getDashboard(): Observable<any> {
    return this.api.get('/api/dashboard');
  }

  getCustomers(params?: Record<string, string | number | boolean | null | undefined>): Observable<PagedResponse<CustomerListItem>> {
    return this.api.get<PagedResponse<CustomerListItem>>('/api/customers', params);
  }

  getCustomer(id: string): Observable<CustomerDetail> {
    return this.api.get<CustomerDetail>(`/api/customers/${id}`);
  }

  createUser(payload: AdminCreateUserPayload): Observable<any> {
    return this.api.post('/api/auth/admin/create-user', payload);
  }

  updateCustomer(id: string, payload: UpdateCustomerPayload): Observable<any> {
    return this.api.put(`/api/customers/${id}`, payload);
  }

  activateCustomer(id: string): Observable<any> {
    return this.api.post(`/api/customers/${id}/activate`, {});
  }

  deactivateCustomer(id: string): Observable<any> {
    return this.api.post(`/api/customers/${id}/deactivate`, {});
  }

  verifyKyc(payload: unknown): Observable<any> {
    return this.api.post('/api/auth/kyc/verify', payload);
  }

  rejectKyc(payload: unknown): Observable<any> {
    return this.api.post('/api/auth/kyc/reject', payload);
  }
}
