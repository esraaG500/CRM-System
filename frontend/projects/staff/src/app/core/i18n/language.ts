import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Translation, TranslocoLoader, TranslocoService } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { Language, NamedRef } from '../api/models';

const STORAGE_KEY = 'crm.lang';

@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);

  getTranslation(lang: string): Observable<Translation> {
    return this.http.get<Translation>(`/i18n/${lang}.json`);
  }
}

/** Current UI language; switching it updates translations, <html lang/dir> and Material's direction. */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);
  private readonly _lang = signal<Language>(this.initial());

  readonly lang = this._lang.asReadonly();
  readonly isRtl = computed(() => this._lang() === 'ar');

  constructor() {
    this.apply(this._lang());
  }

  set(lang: Language): void {
    this._lang.set(lang);
    try { localStorage.setItem(STORAGE_KEY, lang); } catch { /* storage unavailable */ }
    this.apply(lang);
  }

  toggle(): void { this.set(this._lang() === 'ar' ? 'en' : 'ar'); }

  /** Picks the bilingual name that matches the UI language. */
  name(ref: Pick<NamedRef, 'nameEn' | 'nameAr'> | null | undefined): string {
    if (!ref) return '';
    return this._lang() === 'ar' ? ref.nameAr || ref.nameEn : ref.nameEn || ref.nameAr;
  }

  /** Text direction for the root [dir] directive and dialogs. */
  readonly direction = computed<'rtl' | 'ltr'>(() => (this.isRtl() ? 'rtl' : 'ltr'));

  private apply(lang: Language): void {
    this.document.documentElement.lang = lang;
    this.document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
    this.transloco.setActiveLang(lang);
  }

  private initial(): Language {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored === 'ar' || stored === 'en') return stored;
    } catch { /* storage unavailable */ }
    return 'en';
  }
}
