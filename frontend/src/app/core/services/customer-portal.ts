import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api';
import { CustomerDashboard, Profile } from '../models';

@Injectable({ providedIn: 'root' })
export class CustomerPortalService {
  private readonly api = inject(ApiService);

  getDashboard(): Observable<CustomerDashboard> {
    return this.api.get<CustomerDashboard>('/api/portal/customer/dashboard');
  }

  getProfile(): Observable<Profile> {
    return this.api.get<Profile>('/api/portal/profile');
  }

  updateProfile(payload: unknown): Observable<Profile> {
    return this.api.put<Profile>('/api/portal/profile', payload);
  }

  uploadDocuments(formData: FormData): Observable<Profile> {
    return this.api.postForm<Profile>('/api/portal/profile/documents', formData);
  }
}
