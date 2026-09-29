import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoService } from '@jsverse/transloco';

@Injectable({ providedIn: 'root' })
export class Notifier {
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslocoService);

  successKey(key: string, params?: Record<string, unknown>): void {
    this.snackBar.open(this.i18n.translate(key, params), undefined, { duration: 3000 });
  }

  errorKey(key: string, params?: Record<string, unknown>): void {
    this.show(this.i18n.translate(key, params));
  }

  /** Shows the server's message when present, otherwise the fallback translation. */
  error(message: string, fallbackKey: string): void {
    this.show(message || this.i18n.translate(fallbackKey));
  }

  private show(message: string): void {
    this.snackBar.open(message, this.i18n.translate('common.dismiss'), { duration: 6000, panelClass: 'crm-snack-error' });
  }
}
