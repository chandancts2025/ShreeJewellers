import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl =
    (globalThis as { __appApiUrl?: string }).__appApiUrl ??
    (window.location.hostname === 'localhost' ? 'https://localhost:56655' : '');

  get<T>(path: string, params?: Record<string, string | number | boolean | null | undefined>): Observable<T> {
    return this.http.get<T>(`${this.baseUrl}${path}`, { params: this.buildParams(params) });
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
