import { Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { TranslocoService } from '@jsverse/transloco';

/** Translates the table pager and refreshes it when the language changes. */
@Injectable()
export class TranslatedPaginatorIntl extends MatPaginatorIntl {
  private readonly i18n = inject(TranslocoService);

  constructor() {
    super();
    this.i18n.selectTranslation().pipe(takeUntilDestroyed()).subscribe(() => {
      this.itemsPerPageLabel = this.i18n.translate('common.itemsPerPage');
      this.nextPageLabel = this.i18n.translate('common.next');
      this.previousPageLabel = this.i18n.translate('common.previous');
      this.firstPageLabel = this.i18n.translate('common.first');
      this.lastPageLabel = this.i18n.translate('common.last');
      this.changes.next();
    });
  }

  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (length === 0) {
      return this.i18n.translate('common.pageOf', { from: 0, to: 0, total: 0 });
    }
    const from = page * pageSize + 1;
    const to = Math.min(length, (page + 1) * pageSize);
    return this.i18n.translate('common.pageOf', { from, to, total: length });
  };
}
