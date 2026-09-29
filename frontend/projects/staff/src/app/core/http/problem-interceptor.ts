import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ProblemDetails } from '../api/models';
import { Notifier } from './notifier';

/**
 * Shows a message for server errors the calling page does not handle itself.
 * 400 validation errors are left to forms (see formErrors), 401 to the auth interceptor.
 */
export const problemInterceptor: HttpInterceptorFn = (req, next) => {
  const notifier = inject(Notifier);
  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && req.url.startsWith('/api/') && !req.url.startsWith('/api/v1/auth/')) {
        const problem = (error.error ?? {}) as ProblemDetails;
        if (error.status === 0) {
          notifier.errorKey('errors.offline');
        } else if (error.status === 403) {
          notifier.errorKey('errors.forbidden');
        } else if (error.status === 409) {
          notifier.error(problem.detail ?? '', 'errors.conflict');
        } else if (error.status === 400 && !problem.errors) {
          notifier.error(problem.detail ?? '', 'errors.generic');
        } else if (error.status >= 500) {
          notifier.errorKey('errors.server', { id: problem.correlationId ?? '' });
        }
      }
      return throwError(() => error);
    }),
  );
};

/** Extracts field errors from a 400 ValidationProblemDetails. */
export function formErrors(error: unknown): Record<string, string[]> {
  return error instanceof HttpErrorResponse && error.status === 400 ? ((error.error as ProblemDetails)?.errors ?? {}) : {};
}
