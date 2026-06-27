import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth';
import { ToastService } from '../services/toast';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const toasts = inject(ToastService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && auth.isAuthenticated()) {
        auth.clearSession();
      }

      const message =
        error.error?.message ??
        error.error?.detail ??
        (Array.isArray(error.error?.errors) ? error.error.errors.join(', ') : null) ??
        'Please try again.';

      toasts.error(error.status === 401 ? 'Session expired' : 'Request failed', message);
      return throwError(() => error);
    })
  );
};
