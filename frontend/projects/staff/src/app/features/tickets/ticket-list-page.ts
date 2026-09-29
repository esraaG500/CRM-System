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
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { CrmApi } from '../../core/api/crm-api';
import { PagedResult, TICKET_PRIORITIES, TICKET_STATUSES, Ticket, TicketPriority, TicketStatus, TicketSummary } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth-store';
import { Notifier } from '../../core/http/notifier';
import { LanguageService } from '../../core/i18n/language';
import { LookupsStore } from '../../core/lookups-store';
import { AgoPipe } from '../../shared/date-pipes';
import { PriorityLabel, StatusChip } from '../../shared/ticket-badges';
import { CreateTicketDialog } from './create-ticket-dialog';

type View = 'mine' | 'unassigned' | 'all';
const OPEN_STATUSES: TicketStatus[] = ['New', 'Open', 'InProgress', 'PendingCustomer'];

@Component({
  selector: 'crm-ticket-list-page',
  imports: [ReactiveFormsModule, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatIconModule,
    MatPaginatorModule, MatProgressBarModule, TranslocoPipe, AgoPipe, PriorityLabel, StatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="crm-page">
      <div class="crm-page-head">
        <h1>{{ 'tickets.title' | transloco }}</h1>
        @if (canManage) {
          <button mat-flat-button type="button" (click)="create()">
            <mat-icon fontSet="material-symbols-outlined">add</mat-icon>{{ 'tickets.new' | transloco }}
          </button>
        }
      </div>

      <div class="filters">
        <mat-button-toggle-group [value]="view()" (change)="setView($event.value)" [attr.aria-label]="'tickets.filter.view' | transloco">
          <mat-button-toggle value="mine">{{ 'tickets.filter.mine' | transloco }}</mat-button-toggle>
          <mat-button-toggle value="unassigned">{{ 'tickets.filter.unassigned' | transloco }}</mat-button-toggle>
          <mat-button-toggle value="all">{{ 'tickets.filter.all' | transloco }}</mat-button-toggle>
        </mat-button-toggle-group>
        <mat-form-field class="search">
          <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
          <mat-label>{{ 'common.search' | transloco }}</mat-label>
          <input matInput [formControl]="q" [placeholder]="'tickets.searchPlaceholder' | transloco" />
        </mat-form-field>
        <mat-form-field class="narrow">
          <mat-label>{{ 'tickets.filter.status' | transloco }}</mat-label>
          <mat-select [formControl]="status" multiple>
            <!-- Own trigger text: the default one is built before translations load and stays blank. -->
            <mat-select-trigger>{{ selectedLabels('status', status.value) }}</mat-select-trigger>
            @for (s of statuses; track s) { <mat-option [value]="s">{{ 'status.' + s | transloco }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="narrow">
          <mat-label>{{ 'tickets.filter.priority' | transloco }}</mat-label>
          <mat-select [formControl]="priority" multiple>
            <mat-select-trigger>{{ selectedLabels('priority', priority.value) }}</mat-select-trigger>
            @for (p of priorities; track p) { <mat-option [value]="p">{{ 'priority.' + p | transloco }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="narrow">
          <mat-label>{{ 'tickets.filter.department' | transloco }}</mat-label>
          <mat-select [formControl]="department">
            <mat-option [value]="null">{{ 'common.all' | transloco }}</mat-option>
            @for (d of lookups.lookups()?.departments ?? []; track d.id) { <mat-option [value]="d.id">{{ language.name(d) }}</mat-option> }
          </mat-select>
        </mat-form-field>
      </div>

      <section class="crm-panel">
        @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
        @if (result(); as r) {
          @if (r.items.length) {
            <table class="crm-table">
              <thead>
                <tr>
                  <th>{{ 'tickets.col.reference' | transloco }}</th>
                  <th>{{ 'tickets.col.subject' | transloco }}</th>
                  <th class="crm-hide-sm">{{ 'tickets.col.customer' | transloco }}</th>
                  <th>{{ 'tickets.col.priority' | transloco }}</th>
                  <th>{{ 'tickets.col.status' | transloco }}</th>
                  <th class="crm-hide-sm">{{ 'tickets.col.assignee' | transloco }}</th>
                  <th class="crm-hide-sm">{{ 'tickets.col.updated' | transloco }}</th>
                </tr>
              </thead>
              <tbody>
                @for (t of r.items; track t.id) {
                  <tr (click)="open(t)" (keydown.enter)="open(t)" tabindex="0">
                    <td class="crm-edge crm-num crm-muted ref" [class]="'crm-edge-' + t.priority">{{ t.referenceNumber }}</td>
                    <td class="subject" dir="auto">{{ t.subject }}</td>
                    <td class="crm-hide-sm">{{ t.customer.name }}</td>
                    <td><crm-priority [priority]="t.priority" /></td>
                    <td><crm-status-chip [status]="t.status" /></td>
                    <td class="crm-hide-sm">{{ t.assignee?.fullName ?? ('common.unassigned' | transloco) }}</td>
                    <td class="crm-hide-sm crm-muted">{{ (t.updatedAt ?? t.createdAt) | crmAgo }}</td>
                  </tr>
                }
              </tbody>
            </table>
            <mat-paginator [length]="r.totalCount" [pageIndex]="r.page - 1" [pageSize]="r.pageSize" [pageSizeOptions]="[25, 50, 100]"
                           (page)="changePage($event)" />
          } @else {
            <p class="crm-empty">{{ 'tickets.empty' | transloco }}</p>
          }
        }
      </section>
    </div>
  `,
  styles: `
    .filters { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; margin-block-end: 16px; }
    .search { flex: 1; min-width: 240px; }
    .narrow { width: 180px; }
    .ref { white-space: nowrap; font-size: var(--crm-text-s); }
    .subject { font-weight: 500; max-width: 420px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  `,
})
export class TicketListPage {
  private readonly api = inject(CrmApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);
  protected readonly lookups = inject(LookupsStore);
  protected readonly language = inject(LanguageService);
  private readonly i18n = inject(TranslocoService);
  protected readonly canManage = inject(AuthStore).can('tickets.manage');

  protected readonly statuses = TICKET_STATUSES;
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly view = signal<View>('mine');
  protected readonly result = signal<PagedResult<TicketSummary> | null>(null);
  protected readonly loading = signal(false);

  protected readonly q = new FormControl('', { nonNullable: true });
  protected readonly status = new FormControl<TicketStatus[]>(OPEN_STATUSES, { nonNullable: true });
  protected readonly priority = new FormControl<TicketPriority[]>([], { nonNullable: true });
  protected readonly department = new FormControl<string | null>(null);
  private page = 1;
  private pageSize = 25;

  constructor() {
    const params = this.route.snapshot.queryParamMap;
    const view = params.get('view');
    if (view === 'mine' || view === 'unassigned' || view === 'all') this.view.set(view);
    const status = params.getAll('status').filter((s): s is TicketStatus => (TICKET_STATUSES as string[]).includes(s));
    if (status.length) this.status.setValue(status, { emitEvent: false });

    const destroyRef = inject(DestroyRef);
    this.q.valueChanges.pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(destroyRef)).subscribe(() => this.reload());
    for (const control of [this.status, this.priority, this.department] as FormControl<unknown>[]) {
      control.valueChanges.pipe(takeUntilDestroyed(destroyRef)).subscribe(() => this.reload());
    }
    this.load();
  }

  /** e.g. "Open, In progress"; collapses to "Open +3" when many are selected. */
  protected selectedLabels(prefix: 'status' | 'priority', values: readonly string[]): string {
    const labels = values.map(v => this.i18n.translate(prefix + '.' + v));
    return labels.length > 2 ? labels[0] + ' +' + (labels.length - 1) : labels.join(', ');
  }

  protected setView(view: View): void {
    this.view.set(view);
    void this.router.navigate([], { queryParams: { view }, replaceUrl: true });
    this.reload();
  }

  protected changePage(e: PageEvent): void {
    this.page = e.pageIndex + 1;
    this.pageSize = e.pageSize;
    this.load();
  }

  protected open(t: TicketSummary): void {
    void this.router.navigate(['/tickets', t.id]);
  }

  protected create(): void {
    this.dialog.open<CreateTicketDialog, null, Ticket>(CreateTicketDialog, { direction: this.language.direction() })
      .afterClosed().subscribe(ticket => {
        if (ticket) {
          this.notifier.successKey('tickets.created', { ref: ticket.referenceNumber });
          void this.router.navigate(['/tickets', ticket.id]);
        }
      });
  }

  private reload(): void {
    this.page = 1;
    this.load();
  }

  private load(): void {
    const assigneeId = this.view() === 'mine' ? 'me' : this.view() === 'unassigned' ? 'unassigned' : undefined;
    this.loading.set(true);
    this.api.listTickets({
      q: this.q.value.trim(),
      status: this.status.value,
      priority: this.priority.value,
      departmentId: this.department.value ?? undefined,
      assigneeId,
      sort: this.view() === 'unassigned' ? 'priority' : '-updatedAt',
      page: this.page,
      pageSize: this.pageSize,
    }).subscribe({
      next: r => { this.result.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
