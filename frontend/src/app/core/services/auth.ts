import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AuthSession } from '../models';
import { ApiService } from './api';
import { StorageService } from './storage';
import { ToastService } from './toast';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly storage = inject(StorageService);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  private readonly sessionState = signal<AuthSession | null>(this.storage.get<AuthSession>('shree_session'));

  readonly session = computed(() => this.sessionState());
  readonly isAuthenticated = computed(() => !!this.sessionState()?.accessToken);
  readonly roles = computed(() => this.sessionState()?.roles ?? []);

  login(payload: { username: string; password: string; rememberMe: boolean }): Observable<AuthSession> {
    return this.api.post<AuthSession>('/api/auth/login', payload).pipe(
      tap((session) => {
        this.persistSession(session);
        this.toasts.success('Welcome back', `Signed in as ${session.fullName}.`);
        this.navigateAfterLogin();
      })
    );
  }

  forgotPassword(email: string): Observable<{ message: string }> {
    return this.api.post<{ message: string }>('/api/auth/forgot-password', { email });
  }

  changePassword(payload: { currentPassword: string; newPassword: string; confirmPassword: string }): Observable<{ message: string }> {
    return this.api.post<{ message: string }>('/api/auth/change-password', payload);
  }

  logout(): void {
    const refreshToken = this.sessionState()?.refreshToken;
    this.clearSession();
    if (refreshToken) {
      this.api.post('/api/auth/logout', { refreshToken }).subscribe({ error: () => undefined });
    }
    this.router.navigate(['/login']);
  }

  hasAnyRole(expected: string[]): boolean {
    return expected.some((role) => this.roles().includes(role));
  }

  accessToken(): string | null {
    return this.sessionState()?.accessToken ?? null;
  }

  clearSession(): void {
    this.sessionState.set(null);
    this.storage.remove('shree_session');
  }

  persistSession(session: AuthSession): void {
    this.sessionState.set(session);
    this.storage.set('shree_session', session);
  }

  navigateAfterLogin(): void {
    const roles = this.roles();
    this.router.navigateByUrl(roles.includes('Customer') ? '/customer/dashboard' : '/admin/dashboard');
  }
}
