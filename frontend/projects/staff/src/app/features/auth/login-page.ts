import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Language } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth-store';
import { LanguageService } from '../../core/i18n/language';

@Component({
  selector: 'crm-login-page',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <section class="brand-side" aria-hidden="true">
        <div class="brand"><span class="mark"></span>{{ 'app.name' | transloco }}</div>
        <p class="tagline">{{ 'login.tagline' | transloco }}</p>
        <div class="preview">
          <span class="row u"><i></i><b></b><em></em></span>
          <span class="row h"><i></i><b></b><em></em></span>
          <span class="row m"><i></i><b></b><em></em></span>
          <span class="row l"><i></i><b></b><em></em></span>
        </div>
      </section>

      <section class="form-side">
        <div class="lang" role="group" [attr.aria-label]="'nav.languageLabel' | transloco">
          @for (l of languages; track l.code) {
            <button type="button" [class.on]="language.lang() === l.code" [attr.aria-pressed]="language.lang() === l.code"
                    [attr.lang]="l.code" (click)="language.set(l.code)">{{ l.label }}</button>
          }
        </div>

        <form [formGroup]="form" (ngSubmit)="submit()">
          <h1>{{ 'login.title' | transloco }}</h1>
          <p class="crm-muted sub">{{ 'login.subtitle' | transloco }}</p>

          @if (error()) {
            <p class="error" role="alert">{{ error()! | transloco }}</p>
          }

          <mat-form-field>
            <mat-label>{{ 'login.email' | transloco }}</mat-label>
            <input matInput type="email" formControlName="email" autocomplete="username" dir="ltr" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ 'login.password' | transloco }}</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="current-password" dir="ltr" />
          </mat-form-field>

          <button mat-flat-button type="submit" class="submit" [disabled]="busy()">{{ 'login.submit' | transloco }}</button>
        </form>
      </section>
    </div>
  `,
  styles: `
    .page { min-height: 100vh; display: grid; grid-template-columns: minmax(0, 5fr) minmax(0, 6fr); background: var(--crm-surface); }

    .brand-side {
      position: relative; overflow: hidden; display: flex; flex-direction: column; gap: 20px; padding: 40px 48px;
      color: #fff; background:
        radial-gradient(circle at 85% 15%, rgb(255 255 255 / 14%), transparent 40%),
        linear-gradient(160deg, #6a45a3 0%, var(--crm-brand-strong) 55%, #2c1c48 100%);
    }
    .brand { display: flex; align-items: center; gap: 10px; font-weight: 600; font-size: var(--crm-text-l); }
    .mark { width: 28px; height: 28px; border-radius: 9px; background: rgb(255 255 255 / 92%); }
    .tagline { margin: auto 0 0; max-width: 26ch; font-size: 1.75rem; line-height: 1.3; font-weight: 500; letter-spacing: -0.01em; }

    // Abstract ticket queue: the product's priority edges, echoed as decoration.
    .preview {
      display: flex; flex-direction: column; gap: 10px; padding: 16px; border-radius: 16px;
      background: rgb(255 255 255 / 8%); border: 1px solid rgb(255 255 255 / 14%); max-width: 420px;
    }
    .row { display: grid; grid-template-columns: 3px 1fr 48px; gap: 12px; align-items: center; height: 36px; padding-inline-end: 12px;
      border-radius: 8px; background: rgb(255 255 255 / 10%); overflow: hidden; }
    .row i { height: 100%; }
    .row b { height: 8px; border-radius: 4px; background: rgb(255 255 255 / 55%); }
    .row em { height: 14px; border-radius: 7px; background: rgb(255 255 255 / 22%); }
    .row.u i { background: #ff8a65; } .row.u b { width: 80%; }
    .row.h i { background: #ffb74d; } .row.h b { width: 62%; }
    .row.m i { background: #7fb3ec; } .row.m b { width: 70%; }
    .row.l i { background: rgb(255 255 255 / 40%); } .row.l b { width: 48%; }

    .form-side { position: relative; display: grid; place-items: center; padding: 40px 24px; }
    .lang { position: absolute; top: 24px; inset-inline-end: 24px; display: inline-flex; padding: 3px; border-radius: 999px; border: 1px solid var(--crm-rule); }
    .lang button { border: 0; background: transparent; padding: 5px 14px; border-radius: 999px; cursor: pointer; font: 500 var(--crm-text-s) / 1.2 var(--crm-font); color: var(--crm-ink-soft); }
    .lang button[lang='ar'] { font-family: 'IBM Plex Sans Arabic', sans-serif; }
    .lang button.on { background: var(--crm-brand); color: #fff; }

    form { width: min(380px, 100%); display: flex; flex-direction: column; gap: 14px; }
    h1 { font-size: var(--crm-text-2xl); }
    .sub { margin: -6px 0 10px; }
    .error { margin: 0; padding: 10px 12px; border-radius: var(--crm-radius-s); background: #fdecea; color: var(--crm-urgent); }
    .submit { height: 44px; margin-block-start: 4px; }

    @media (max-width: 860px) {
      .page { grid-template-columns: 1fr; }
      .brand-side { display: none; }
    }
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  readonly returnUrl = input<string>('/');

  protected readonly languages: { code: Language; label: string }[] = [
    { code: 'en', label: 'English' },
    { code: 'ar', label: 'عربي' },
  ];

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected submit(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();
    // The UI keeps the language chosen on this screen (English by default).
    this.auth.login(email, password).subscribe({
      next: () => void this.router.navigateByUrl(this.returnUrl() || '/', { replaceUrl: true }),
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        const code = (e.error as { code?: string } | null)?.code;
        this.error.set(code === 'auth.locked' ? 'login.locked' : e.status === 0 ? 'errors.offline' : 'login.failed');
      },
    });
  }
}
