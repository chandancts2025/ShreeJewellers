import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api';
import { EmailSettings } from '../models';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly api = inject(ApiService);

  getShopSettings(): Observable<any> {
    return this.api.get('/api/settings/shop');
  }

  updateShopSettings(payload: unknown): Observable<any> {
    return this.api.put('/api/settings/shop', payload);
  }

  getInterestRates(): Observable<any> {
    return this.api.get('/api/settings/interest-rates');
  }

  createInterestRate(payload: unknown): Observable<any> {
    return this.api.post('/api/settings/interest-rates', payload);
  }

  getUsers(): Observable<any> {
    return this.api.get('/api/settings/users');
  }

  getGoldPriceSettings(): Observable<any> {
    return this.api.get('/api/settings/gold-price');
  }

  updateGoldPriceSettings(payload: unknown): Observable<any> {
    return this.api.put('/api/settings/gold-price', payload);
  }

  getEmailSettings(): Observable<EmailSettings> {
    return this.api.get<EmailSettings>('/api/settings/email');
  }

  sendTestEmail(recipientEmail?: string): Observable<{ success: boolean; message: string }> {
    return this.api.post<{ success: boolean; message: string }>('/api/settings/test-email', { recipientEmail });
  }

  getReport(endpoint: string, params: Record<string, string | number | boolean | null | undefined>): Observable<any> {
    return this.api.get(`/api/reports/${endpoint}`, params);
  }

  downloadReport(endpoint: string, params: Record<string, string | number | boolean | null | undefined>): Observable<Blob> {
    return this.api.getBlob(`/api/reports/${endpoint}`, params);
  }
}
