import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { GoldPrice, PublicAboutData, PublicContactData, PublicGoldLoanInfo, PublicHomeData } from '../models';
import { ApiService } from './api';

@Injectable({ providedIn: 'root' })
export class PublicDataService {
  private readonly api = inject(ApiService);

  getHome(): Observable<PublicHomeData> {
    return this.api.get<PublicHomeData>('/api/public/home');
  }

  getAbout(): Observable<PublicAboutData> {
    return this.api.get<PublicAboutData>('/api/public/about');
  }

  getContact(): Observable<PublicContactData> {
    return this.api.get<PublicContactData>('/api/public/contact');
  }

  getGoldLoanInfo(): Observable<PublicGoldLoanInfo> {
    return this.api.get<PublicGoldLoanInfo>('/api/public/gold-loan-info');
  }

  getPrices(): Observable<GoldPrice> {
    return this.api.get<GoldPrice>('/api/prices/current');
  }

  createContactInquiry(payload: { name: string; phone: string; email?: string; message: string }): Observable<{ message: string }> {
    return this.api.post<{ message: string }>('/api/public/contact-inquiry', payload);
  }
}
