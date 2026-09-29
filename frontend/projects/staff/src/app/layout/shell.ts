import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { filter, map } from 'rxjs';
import { Language } from '../core/api/models';
import { AuthStore } from '../core/auth/auth-store';
import { LanguageService } from '../core/i18n/language';
import { LookupsStore } from '../core/lookups-store';

interface NavItem { path: string; icon: string; label: string; exact: boolean; }

/**
 * App frame. A CSS grid places the navigation on the inline-start side, so it follows the text
 * direction automatically when the language changes (no measured offsets that can go stale).
 */
@Component({
  selector: 'crm-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, MatIconModule, MatMenuModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="frame" [class.compact]="compact()">
      <aside class="rail" [class.open]="menuOpen()" [attr.aria-hidden]="compact() && !menuOpen()">
        <a class="brand" routerLink="/" (click)="menuOpen.set(false)">
          <span class="mark" aria-hidden="true"></span>
          <span>{{ 'app.name' | transloco }}</span>
        </a>
        <nav>
          @for (item of nav; track item.path) {
            <a [routerLink]="item.path" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: item.exact }"
               #rla="routerLinkActive" [attr.aria-current]="rla.isActive ? 'page' : null" (click)="menuOpen.set(false)">
              <mat-icon fontSet="material-symbols-outlined" [class.filled]="rla.isActive">{{ item.icon }}</mat-icon>
              <span>{{ item.label | transloco }}</span>
            </a>
          }
        </nav>
      </aside>

      @if (compact() && menuOpen()) {
        <button type="button" class="scrim" (click)="menuOpen.set(false)" [attr.aria-label]="'common.dismiss' | transloco"></button>
      }

      <div class="content">
        <header class="topbar">
          @if (compact()) {
            <button mat-icon-button type="button" (click)="menuOpen.set(true)" [attr.aria-label]="'nav.menu' | transloco">
              <mat-icon fontSet="material-symbols-outlined">menu</mat-icon>
            </button>
          }
          <span class="spacer"></span>

          <div class="lang" role="group" [attr.aria-label]="'nav.languageLabel' | transloco">
            @for (l of languages; track l.code) {
              <button type="button" [class.on]="language.lang() === l.code" [attr.aria-pressed]="language.lang() === l.code"
                      [attr.lang]="l.code" (click)="language.set(l.code)">{{ l.label }}</button>
            }
          </div>

          <button type="button" class="user" [matMenuTriggerFor]="userMenu">
            <span class="avatar" aria-hidden="true">{{ initials() }}</span>
            <span class="who crm-hide-sm">{{ auth.user()?.fullName }}</span>
            <mat-icon fontSet="material-symbols-outlined" class="crm-hide-sm">expand_more</mat-icon>
          </button>
          <mat-menu #userMenu="matMenu" xPosition="before">
            <div class="menu-user">
              <b>{{ auth.user()?.fullName }}</b>
              <span class="crm-muted">{{ auth.user()?.email }}</span>
            </div>
            <button mat-menu-item type="button" (click)="auth.logout()">
              <mat-icon fontSet="material-symbols-outlined">logout</mat-icon>
              {{ 'nav.signOut' | transloco }}
            </button>
          </mat-menu>
        </header>

        <main><router-outlet /></main>
      </div>
    </div>
  `,
  styles: `
    :host { display: block; }
    .frame { display: grid; grid-template-columns: 248px minmax(0, 1fr); min-height: 100vh; }

    .rail {
      position: sticky; top: 0; height: 100vh; overflow-y: auto;
      display: flex; flex-direction: column; gap: 8px; padding: 16px 12px;
      background: var(--crm-surface); border-inline-end: 1px solid var(--crm-rule);
    }
    .brand {
      display: flex; align-items: center; gap: 10px; padding: 8px 12px 20px;
      font-weight: 600; font-size: var(--crm-text-l); color: var(--crm-ink); text-decoration: none;
    }
    .mark {
      width: 28px; height: 28px; border-radius: 9px; flex: none;
      background: linear-gradient(135deg, var(--crm-brand), #8f63c9);
      box-shadow: 0 2px 6px rgb(91 59 140 / 30%);
    }
    nav { display: flex; flex-direction: column; gap: 4px; }
    nav a {
      display: flex; align-items: center; gap: 12px; padding: 10px 12px; border-radius: 10px;
      color: var(--crm-ink-soft); text-decoration: none; font-weight: 500;
      transition: background-color 120ms ease, color 120ms ease;
    }
    nav a:hover { background: var(--crm-surface-2); color: var(--crm-ink); }
    nav a.active { background: var(--crm-brand-soft); color: var(--crm-brand-strong); }
    mat-icon.filled { font-variation-settings: 'FILL' 1; }

    .content { min-width: 0; display: flex; flex-direction: column; }
    .topbar {
      position: sticky; top: 0; z-index: 5; display: flex; align-items: center; gap: 12px;
      height: 60px; padding-inline: 24px;
      background: rgb(246 246 249 / 82%); backdrop-filter: saturate(160%) blur(10px);
      border-block-end: 1px solid var(--crm-rule);
    }
    .spacer { flex: 1; }

    .lang { display: inline-flex; padding: 3px; border-radius: 999px; background: var(--crm-surface); border: 1px solid var(--crm-rule); }
    .lang button {
      border: 0; background: transparent; padding: 5px 14px; border-radius: 999px; cursor: pointer;
      font: 500 var(--crm-text-s) / 1.2 var(--crm-font); color: var(--crm-ink-soft);
    }
    .lang button[lang='ar'] { font-family: 'IBM Plex Sans Arabic', sans-serif; }
    .lang button.on { background: var(--crm-brand); color: #fff; }

    .user {
      display: inline-flex; align-items: center; gap: 8px; padding-block: 4px; padding-inline: 4px 8px; border-radius: 999px;
      border: 1px solid transparent; background: transparent; cursor: pointer; font: inherit; color: var(--crm-ink);
    }
    .user:hover { background: var(--crm-surface); border-color: var(--crm-rule); }
    .avatar {
      display: inline-grid; place-items: center; width: 32px; height: 32px; border-radius: 50%;
      background: var(--crm-brand-soft); color: var(--crm-brand-strong); font-size: var(--crm-text-xs); font-weight: 600;
    }
    .who { max-width: 180px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 500; }
    .menu-user { display: flex; flex-direction: column; padding: 8px 16px 10px; font-size: var(--crm-text-s); }

    // Phone and small tablet: navigation becomes a slide-in drawer on the inline-start side.
    .frame.compact { grid-template-columns: minmax(0, 1fr); }
    .frame.compact .rail {
      position: fixed; inset-block: 0; inset-inline-start: 0; z-index: 20; width: 272px; height: 100%;
      box-shadow: 0 8px 32px rgb(31 26 46 / 18%);
      transform: translateX(-100%); transition: transform 180ms ease, visibility 180ms; visibility: hidden;
    }
    :host-context([dir='rtl']) .frame.compact .rail { transform: translateX(100%); }
    .frame.compact .rail.open, :host-context([dir='rtl']) .frame.compact .rail.open { transform: none; visibility: visible; }
    .scrim { position: fixed; inset: 0; z-index: 15; border: 0; background: rgb(31 26 46 / 32%); cursor: pointer; }
    .frame.compact .topbar { padding-inline: 8px 16px; }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthStore);
  protected readonly language = inject(LanguageService);
  protected readonly menuOpen = signal(false);
  protected readonly compact = toSignal(
    inject(BreakpointObserver).observe('(max-width: 960px)').pipe(map(r => r.matches)),
    { initialValue: false },
  );

  protected readonly languages: { code: Language; label: string }[] = [
    { code: 'en', label: 'English' },
    { code: 'ar', label: 'عربي' },
  ];

  protected readonly nav: NavItem[] = [
    { path: '/', icon: 'space_dashboard', label: 'nav.dashboard', exact: true },
    { path: '/tickets', icon: 'confirmation_number', label: 'nav.tickets', exact: false },
    { path: '/customers', icon: 'group', label: 'nav.customers', exact: false },
  ];

  constructor() {
    inject(LookupsStore).load().subscribe();
    inject(Router).events.pipe(filter(e => e instanceof NavigationEnd), takeUntilDestroyed()).subscribe(() => this.menuOpen.set(false));
  }

  protected initials(): string {
    const name = this.auth.user()?.fullName ?? '';
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map(p => p[0]).join('').toUpperCase();
  }
}
