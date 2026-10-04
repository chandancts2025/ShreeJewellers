import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiService } from './api';
import { GoldLoanResponse, GoldLoanSummary, LoanEligibility, OutstandingBalance, RepaymentSchedule, PagedResponse } from '../models';

@Injectable({ providedIn: 'root' })
export class LoanService {
  private readonly api = inject(ApiService);

  getCustomerLoans(customerId: string): Observable<GoldLoanResponse[]> {
    return this.api.get<GoldLoanResponse[]>(`/api/goldloan/customer/${customerId}`);
  }

  getActiveLoans(search = '', page = 1, pageSize = 20): Observable<GoldLoanSummary[]> {
    return this.api.get<GoldLoanSummary[] | PagedResponse<GoldLoanSummary>>('/api/goldloan/active', { search, page, pageSize })
      .pipe(map((response) => Array.isArray(response) ? response : response.data ?? []));
  }

  getLoan(id: number): Observable<GoldLoanResponse> {
    return this.api.get<GoldLoanResponse>(`/api/goldloan/${id}`);
  }

  getOutstanding(id: number): Observable<OutstandingBalance> {
    return this.api.get<OutstandingBalance>(`/api/goldloan/${id}/outstanding-balance`);
  }

  getSchedule(id: number): Observable<RepaymentSchedule> {
    return this.api.get<RepaymentSchedule>(`/api/goldloan/${id}/repayment-schedule`);
  }

  getDueThisWeek(): Observable<GoldLoanSummary[]> {
    return this.api.get<GoldLoanSummary[] | PagedResponse<GoldLoanSummary>>('/api/goldloan/due-this-week')
      .pipe(map((response) => Array.isArray(response) ? response : response.data ?? []));
  }

  getDefaulted(): Observable<GoldLoanSummary[]> {
    return this.api.get<GoldLoanSummary[] | PagedResponse<GoldLoanSummary>>('/api/goldloan/defaulted')
      .pipe(map((response) => Array.isArray(response) ? response : response.data ?? []));
  }

  checkEligibility(payload: unknown): Observable<LoanEligibility> {
    return this.api.post<LoanEligibility>('/api/goldloan/eligibility', payload);
  }

  createLoan(payload: unknown): Observable<GoldLoanResponse> {
    return this.api.post<GoldLoanResponse>('/api/goldloan', payload);
  }

  recordRepayment(id: number, payload: unknown): Observable<any> {
    return this.api.post(`/api/goldloan/${id}/repayment`, payload);
  }

  getRepayments(id: number): Observable<any> {
    return this.api.get(`/api/goldloan/${id}/repayments`);
  }

  extendLoan(id: number, payload: unknown): Observable<any> {
    return this.api.post(`/api/goldloan/${id}/extend`, payload);
  }

  closeLoan(id: number, payload: unknown): Observable<any> {
    return this.api.post(`/api/goldloan/${id}/close`, payload);
  }

  auctionLoan(id: number, payload: unknown): Observable<any> {
    return this.api.post(`/api/goldloan/${id}/auction`, payload);
  }

  checkDefaults(): Observable<{ message: string }> {
    return this.api.post<{ message: string }>('/api/goldloan/check-defaults', {});
  }
}
