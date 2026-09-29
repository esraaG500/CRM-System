import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { AuthResult, CurrentUser } from '../api/models';

/**
 * Holds the session. The access token lives only in memory; the refresh token is an HttpOnly cookie
 * the browser sends to /api/v1/auth/refresh, so a page reload restores the session without exposing tokens to JS.
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _user = signal<CurrentUser | null>(null);
  private accessToken: string | null = null;
  private refreshInFlight: Observable<string | null> | null = null;

  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this._user() !== null);
  readonly permissions = computed(() => new Set(this._user()?.permissions ?? []));

  get token(): string | null { return this.accessToken; }

  can(permission: string): boolean { return this.permissions().has(permission); }

  login(email: string, password: string): Observable<CurrentUser> {
    return this.http.post<AuthResult>('/api/v1/auth/login', { email, password }).pipe(
      tap(result => this.apply(result)),
      map(result => result.user),
    );
  }

  /** Exchanges the refresh cookie for a new access token. Concurrent callers share one request. */
  refresh(): Observable<string | null> {
    this.refreshInFlight ??= this.http.post<AuthResult>('/api/v1/auth/refresh', {}).pipe(
      tap(result => this.apply(result)),
      map(result => result.accessToken as string | null),
      catchError(() => {
        this.clear();
        return of(null);
      }),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  logout(): void {
    this.http.post('/api/v1/auth/logout', {}).pipe(catchError(() => of(null))).subscribe(() => {
      this.clear();
      void this.router.navigate(['/login']);
    });
  }

  /** Called when the session cannot be recovered. */
  expire(): void {
    this.clear();
    void this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
  }

  private apply(result: AuthResult): void {
    this.accessToken = result.accessToken;
    this._user.set(result.user);
  }

  private clear(): void {
    this.accessToken = null;
    this._user.set(null);
  }
}
