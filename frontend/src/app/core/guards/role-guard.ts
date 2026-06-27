import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth';

export const roleGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const roles = (route.data['roles'] as string[] | undefined) ?? [];

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  return roles.length === 0 || auth.hasAnyRole(roles) ? true : router.createUrlTree(['/']);
};
