import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private getBaseUrl(): string {
    const globalUrl = (globalThis as { __appApiUrl?: string }).__appApiUrl;
    if (globalUrl) return globalUrl;

    if (typeof window !== 'undefined') {
      const stored = window.localStorage?.getItem('API_URL');
      if (stored) return stored;

      if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
        return 'https://localhost:56655';
      }

      const metaUrl = document.querySelector('meta[name="api-url"]')?.getAttribute('content');
      if (metaUrl) return metaUrl.replace(/\/+$/, '');
    }

    return 'https://shreejewellers-api.onrender.com';
  }

  private readonly baseUrl = this.getBaseUrl();

  get<T>(path: string, params?: Record<string, string | number | boolean | null | undefined>): Observable<T> {
    return this.http.get<T>(`${this.baseUrl}${path}`, { params: this.buildParams(params) });
  }

  getBlob(path: string, params?: Record<string, string | number | boolean | null | undefined>): Observable<Blob> {
    return this.http.get(`${this.baseUrl}${path}`, {
      params: this.buildParams(params),
      responseType: 'blob'
    });
  }

  post<T>(path: string, body: unknown, params?: Record<string, string | number | boolean | null | undefined>): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}${path}`, body, { params: this.buildParams(params) });
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(`${this.baseUrl}${path}`, body);
  }

  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(`${this.baseUrl}${path}`);
  }

  postForm<T>(path: string, body: FormData): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}${path}`, body);
  }

  private buildParams(input?: Record<string, string | number | boolean | null | undefined>): HttpParams {
    let params = new HttpParams();
    if (!input) {
      return params;
    }

    Object.entries(input).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    });

    return params;
  }
}
