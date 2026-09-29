import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, isDevMode, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { MAT_DIALOG_DEFAULT_OPTIONS } from '@angular/material/dialog';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MAT_PAGINATOR_DEFAULT_OPTIONS, MatPaginatorIntl } from '@angular/material/paginator';
import { provideRouter, withComponentInputBinding, withRouterConfig } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth-interceptor';
import { problemInterceptor } from './core/http/problem-interceptor';
import { TranslocoHttpLoader } from './core/i18n/language';
import { TranslatedPaginatorIntl } from './core/i18n/paginator-intl';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding(), withRouterConfig({ paramsInheritanceStrategy: 'always' })),
    provideHttpClient(withInterceptors([authInterceptor, problemInterceptor])),
    provideTransloco({
      config: {
        availableLangs: ['ar', 'en'],
        defaultLang: 'en',
        fallbackLang: 'en',
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
      },
      loader: TranslocoHttpLoader,
    }),
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'fill', subscriptSizing: 'dynamic', floatLabel: 'auto' } },
    { provide: MatPaginatorIntl, useClass: TranslatedPaginatorIntl },
    { provide: MAT_PAGINATOR_DEFAULT_OPTIONS, useValue: { formFieldAppearance: 'fill' } },
    { provide: MAT_DIALOG_DEFAULT_OPTIONS, useValue: { maxWidth: '95vw', autoFocus: 'first-tabbable', restoreFocus: true, hasBackdrop: true } },
  ],
};
