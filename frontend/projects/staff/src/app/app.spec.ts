import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { AuthStore } from './core/auth/auth-store';
import { authInterceptor } from './core/auth/auth-interceptor';
import { LanguageService } from './core/i18n/language';
import { PriorityLabel } from './shared/ticket-badges';

const user = {
  id: 'u1', fullName: 'Omar Al-Harbi', email: 'agent1@crm.local', userType: 'Staff', roles: ['Agent'],
  permissions: ['tickets.view'], departmentIds: [], branchId: null, preferredLanguage: 'ar' as const,
};

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthStore);
  });

  afterEach(() => backend.verify());

  it('adds the bearer token after sign-in', () => {
    auth.login('agent1@crm.local', 'secret').subscribe();
    backend.expectOne('/api/v1/auth/login').flush({ accessToken: 'token-1', expiresIn: 900, user });

    http.get('/api/v1/customers').subscribe();

    expect(backend.expectOne('/api/v1/customers').request.headers.get('Authorization')).toBe('Bearer token-1');
  });

  it('refreshes once on 401 and retries the original request with the new token', () => {
    let body: unknown;
    http.get('/api/v1/tickets').subscribe(b => (body = b));

    backend.expectOne('/api/v1/tickets').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/v1/auth/refresh').flush({ accessToken: 'token-2', expiresIn: 900, user });
    const retry = backend.expectOne('/api/v1/tickets');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer token-2');
    retry.flush({ items: [] });

    expect(body).toEqual({ items: [] });
    expect(auth.user()?.fullName).toBe('Omar Al-Harbi');
  });

  it('does not attach tokens to non-API requests', () => {
    http.get('/i18n/ar.json').subscribe();
    expect(backend.expectOne('/i18n/ar.json').request.headers.has('Authorization')).toBeFalse();
  });
});

describe('LanguageService', () => {
  beforeEach(() => {
    localStorage.removeItem('crm.lang');
    TestBed.configureTestingModule({ imports: [TranslocoTestingModule.forRoot({ langs: { ar: {}, en: {} } })] });
  });

  it('defaults to English left-to-right', () => {
    const language = TestBed.inject(LanguageService);
    expect(language.lang()).toBe('en');
    expect(document.documentElement.dir).toBe('ltr');
    expect(language.direction()).toBe('ltr');
  });

  it('switches to Arabic right-to-left and remembers the choice', () => {
    const language = TestBed.inject(LanguageService);
    language.toggle();
    expect(document.documentElement.dir).toBe('rtl');
    expect(document.documentElement.lang).toBe('ar');
    expect(localStorage.getItem('crm.lang')).toBe('ar');
  });

  it('picks the bilingual name for the current language', () => {
    const language = TestBed.inject(LanguageService);
    const ref = { nameEn: 'Billing', nameAr: 'الفوترة' };
    expect(language.name(ref)).toBe('Billing');
    language.set('ar');
    expect(language.name(ref)).toBe('الفوترة');
  });
});

describe('PriorityLabel', () => {
  it('renders the translated priority with its data attribute', async () => {
    await TestBed.configureTestingModule({
      imports: [PriorityLabel, TranslocoTestingModule.forRoot({ langs: { en: { priority: { Urgent: 'Urgent' } } }, translocoConfig: { defaultLang: 'en', availableLangs: ['en'] }, preloadLangs: true })],
    }).compileComponents();

    const fixture = TestBed.createComponent(PriorityLabel);
    fixture.componentRef.setInput('priority', 'Urgent');
    fixture.detectChanges();

    const el = fixture.nativeElement.querySelector('[data-priority]') as HTMLElement;
    expect(el.dataset['priority']).toBe('Urgent');
    expect(el.textContent?.trim()).toBe('Urgent');
  });
});
