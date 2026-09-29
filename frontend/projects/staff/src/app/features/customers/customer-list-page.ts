import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { CrmApi } from '../../core/api/crm-api';
import { Customer, CustomerSummary, CustomerType, PagedResult } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth-store';
import { Notifier } from '../../core/http/notifier';
import { LanguageService } from '../../core/i18n/language';
import { CustomerFormDialog } from './customer-form-dialog';

@Component({
  selector: 'crm-customer-list-page',
  imports: [ReactiveFormsModule, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatInputModule, MatIconModule, MatPaginatorModule, MatProgressBarModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="crm-page">
      <div class="crm-page-head">
        <h1>{{ 'customers.title' | transloco }}</h1>
        @if (canManage) {
          <button mat-flat-button type="button" (click)="create()">
            <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>{{ 'customers.new' | transloco }}
          </button>
        }
      </div>

      <div class="filters">
        <mat-form-field class="search">
          <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
          <mat-label>{{ 'common.search' | transloco }}</mat-label>
          <input matInput [formControl]="q" [placeholder]="'customers.searchPlaceholder' | transloco" />
        </mat-form-field>
        <mat-button-toggle-group [value]="type()" (change)="setType($event.value)">
          <mat-button-toggle [value]="null">{{ 'common.all' | transloco }}</mat-button-toggle>
          <mat-button-toggle value="Company">{{ 'customers.type.Company' | transloco }}</mat-button-toggle>
          <mat-button-toggle value="Individual">{{ 'customers.type.Individual' | transloco }}</mat-button-toggle>
        </mat-button-toggle-group>
      </div>

      <section class="crm-panel">
        @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
        @if (result(); as r) {
          @if (r.items.length) {
            <table class="crm-table">
              <thead>
                <tr>
                  <th>{{ 'customers.col.name' | transloco }}</th>
                  <th>{{ 'customers.col.reference' | transloco }}</th>
                  <th class="crm-hide-sm">{{ 'customers.col.email' | transloco }}</th>
                  <th class="crm-hide-sm">{{ 'customers.col.phone' | transloco }}</th>
                  <th>{{ 'customers.col.open' | transloco }}</th>
                </tr>
              </thead>
              <tbody>
                @for (c of r.items; track c.id) {
                  <tr (click)="open(c)" (keydown.enter)="open(c)" tabindex="0">
                    <td>
                      <span class="name">{{ c.name }}</span>
                      <span class="type crm-muted">{{ 'customers.type.' + c.type | transloco }}</span>
                      @if (c.needsReview) { <span class="review">{{ 'customers.needsReview' | transloco }}</span> }
                    </td>
                    <td class="crm-num crm-muted">{{ c.referenceNumber }}</td>
                    <td class="crm-hide-sm" dir="ltr">{{ c.primaryEmail }}</td>
                    <td class="crm-hide-sm crm-num" dir="ltr">{{ c.primaryPhone }}</td>
                    <td class="crm-num">{{ c.openTicketCount || '' }}</td>
                  </tr>
                }
              </tbody>
            </table>
            <mat-paginator [length]="r.totalCount" [pageIndex]="r.page - 1" [pageSize]="r.pageSize" [pageSizeOptions]="[25, 50, 100]"
                           (page)="changePage($event)" />
          } @else {
            <p class="crm-empty">{{ (q.value ? 'customers.empty' : 'customers.emptyStart') | transloco }}</p>
          }
        }
      </section>
    </div>
  `,
  styles: `
    .filters { display: flex; gap: 16px; align-items: center; flex-wrap: wrap; margin-block-end: 16px; }
    .search { flex: 1; min-width: 260px; max-width: 520px; }
    .name { font-weight: 500; }
    .type { margin-inline-start: 8px; font-size: var(--crm-text-xs); }
    .review { margin-inline-start: 8px; font-size: var(--crm-text-xs); padding: 1px 6px; border-radius: 999px; background: #fdf1e3; color: #8a4a07; }
    td[dir='ltr'] { text-align: start; }
    :host-context([dir='rtl']) td[dir='ltr'] { text-align: right; }
  `,
})
export class CustomerListPage {
  private readonly api = inject(CrmApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly notifier = inject(Notifier);
  private readonly language = inject(LanguageService);

  protected readonly canManage = inject(AuthStore).can('customers.manage');
  protected readonly q = new FormControl('', { nonNullable: true });
  protected readonly type = signal<CustomerType | null>(null);
  protected readonly result = signal<PagedResult<CustomerSummary> | null>(null);
  protected readonly loading = signal(false);
  private page = 1;
  private pageSize = 25;

  constructor() {
    this.q.valueChanges.pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => { this.page = 1; this.load(); });
    this.load();
  }

  protected setType(type: CustomerType | null): void {
    this.type.set(type);
    this.page = 1;
    this.load();
  }

  protected changePage(e: PageEvent): void {
    this.page = e.pageIndex + 1;
    this.pageSize = e.pageSize;
    this.load();
  }

  protected open(c: CustomerSummary): void {
    void this.router.navigate(['/customers', c.id]);
  }

  protected create(): void {
    this.dialog.open<CustomerFormDialog, null, Customer>(CustomerFormDialog, { direction: this.language.direction(), autoFocus: 'dialog' })
      .afterClosed().subscribe(customer => {
        if (customer) {
          this.notifier.successKey('customers.created');
          void this.router.navigate(['/customers', customer.id]);
        }
      });
  }

  private load(): void {
    this.loading.set(true);
    this.api.searchCustomers(this.q.value.trim(), this.page, this.pageSize, this.type()).subscribe({
      next: r => { this.result.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
