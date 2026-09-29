import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { NavigationHistory } from '../core/navigation-history';

/**
 * Returns to the previous page. When the page was opened directly (new tab, shared link),
 * there is no in-app history, so it navigates to `fallback` instead.
 */
@Component({
  selector: 'crm-back-button',
  imports: [MatButtonModule, MatIconModule, MatTooltipModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button mat-icon-button type="button" class="back" (click)="back()"
            [attr.aria-label]="'common.back' | transloco" [matTooltip]="'common.back' | transloco">
      <mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon>
    </button>
  `,
  styles: `
    :host { display: inline-flex; flex: none; }
    .back { border: 1px solid var(--crm-rule); background: var(--crm-surface); color: var(--crm-ink); }
    .back:hover { border-color: var(--crm-brand); color: var(--crm-brand); }
    // The arrow points toward the start of the reading direction.
    :host-context([dir='rtl']) mat-icon { transform: scaleX(-1); }
  `,
})
export class BackButton {
  private readonly location = inject(Location);
  private readonly router = inject(Router);
  private readonly history = inject(NavigationHistory);

  readonly fallback = input<string>('/');

  protected back(): void {
    if (this.history.canGoBack) {
      this.history.wentBack();
      this.location.back();
    } else {
      void this.router.navigateByUrl(this.fallback());
    }
  }
}
