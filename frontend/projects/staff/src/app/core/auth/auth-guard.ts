import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthStore } from './auth-store';

/** Allows navigation when signed in; otherwise tries the refresh cookie, then sends the user to sign in. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  if (auth.isAuthenticated()) {
    return true;
  }

  return auth.refresh().pipe(
    map(token => token ? true : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } })),
  );
};

/** Route guard factory requiring a permission; redirects to the dashboard when missing. */
export function permissionGuard(permission: string): CanActivateFn {
  return () => inject(AuthStore).can(permission) || inject(Router).createUrlTree(['/']);
}
