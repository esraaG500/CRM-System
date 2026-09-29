import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { CrmApi } from '../../core/api/crm-api';
import { AgentDashboard, TICKET_PRIORITIES, TICKET_STATUSES, TicketSummary } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth-store';
import { AgoPipe } from '../../shared/date-pipes';
import { PriorityLabel, StatusChip } from '../../shared/ticket-badges';

@Component({
  selector: 'crm-ticket-rows',
  imports: [RouterLink, AgoPipe, PriorityLabel, StatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul class="rows">
      @for (t of tickets(); track t.id) {
        <li class="crm-edge" [class]="'crm-edge-' + t.priority">
          <a [routerLink]="['/tickets', t.id]">
            <span class="ref crm-num">{{ t.referenceNumber }}</span>
            <span class="subject" dir="auto">{{ t.subject }}</span>
            <span class="customer crm-muted">{{ t.customer.name }}</span>
            <crm-priority [priority]="t.priority" class="crm-hide-sm" />
            <crm-status-chip [status]="t.status" />
            <span class="ago crm-muted crm-hide-sm">{{ t.createdAt | crmAgo }}</span>
          </a>
        </li>
      }
    </ul>
  `,
  styles: `
    .rows { list-style: none; margin: 0; padding: 0; }
    li { border-block-end: 1px solid var(--crm-rule); }
    li:last-child { border-block-end: 0; }
    a {
      display: grid; grid-template-columns: 92px minmax(0, 2fr) minmax(0, 1fr) 90px auto 88px; gap: 12px; align-items: center;
      padding: 10px 14px; color: inherit; text-decoration: none;
    }
    a:hover { background: var(--crm-brand-soft); }
    .ref { font-size: var(--crm-text-s); color: var(--crm-ink-soft); }
    .subject { font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .customer, .ago { font-size: var(--crm-text-s); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .ago { text-align: end; }
    @media (max-width: 720px) { a { grid-template-columns: 80px minmax(0, 1fr) auto; } .customer { display: none; } }
  `,
})
export class TicketRows {
  readonly tickets = input.required<TicketSummary[]>();
}

@Component({
  selector: 'crm-dashboard-page',
  imports: [RouterLink, MatButtonModule, MatProgressBarModule, TranslocoPipe, TicketRows, PriorityLabel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="crm-page">
      <header class="intro">
        <h1>{{ 'dashboard.greeting' | transloco: { name: firstName() } }}</h1>
        @if (data(); as d) {
          <p class="crm-muted">{{ 'dashboard.summary' | transloco: { open: d.myOpenCount, queue: d.queueUnassignedCount } }}</p>
        }
      </header>

      @if (!data()) {
        <mat-progress-bar mode="indeterminate" />
      }

      @if (data(); as d) {
        <section class="status" [attr.aria-label]="'dashboard.statusBar' | transloco">
          <div class="bar" role="img" [attr.aria-label]="statusSummary()">
            @for (s of statusSegments(); track s.status) {
              <span [attr.data-status]="s.status" [style.flex-grow]="s.count"></span>
            }
          </div>
          <ul class="legend">
            @for (s of statusSegments(); track s.status) {
              <li [attr.data-status]="s.status">
                <a [routerLink]="['/tickets']" [queryParams]="{ view: 'mine', status: s.status }">
                  <i aria-hidden="true"></i>{{ 'status.' + s.status | transloco }} <b class="crm-num">{{ s.count }}</b>
                </a>
              </li>
            }
            <li class="today crm-muted">
              {{ 'dashboard.today' | transloco }}:
              <b class="crm-num">{{ d.newToday }}</b> {{ 'dashboard.newToday' | transloco }},
              <b class="crm-num">{{ d.resolvedToday }}</b> {{ 'dashboard.resolvedToday' | transloco }}
            </li>
          </ul>
        </section>

        <div class="grid">
          <div class="main">
            <section class="crm-panel">
              <div class="panel-head">
                <h2>{{ 'dashboard.myQueue' | transloco }}</h2>
                <a mat-button [routerLink]="['/tickets']" [queryParams]="{ view: 'mine' }">{{ 'dashboard.viewAll' | transloco }}</a>
              </div>
              @if (d.assigned.length) {
                <crm-ticket-rows [tickets]="d.assigned" />
              } @else {
                <p class="crm-empty">{{ 'dashboard.myQueueEmpty' | transloco }}</p>
              }
            </section>

            <section class="crm-panel">
              <div class="panel-head"><h2>{{ 'dashboard.awaiting' | transloco }}</h2></div>
              @if (d.awaitingFirstResponse.length) {
                <crm-ticket-rows [tickets]="d.awaitingFirstResponse" />
              } @else {
                <p class="crm-empty">{{ 'dashboard.awaitingEmpty' | transloco }}</p>
              }
            </section>
          </div>

          <aside class="side">
            <section class="crm-panel">
              <div class="panel-head">
                <h2>{{ 'dashboard.unassigned' | transloco }} <span class="count crm-num">{{ d.queueUnassignedCount }}</span></h2>
                <a mat-button [routerLink]="['/tickets']" [queryParams]="{ view: 'unassigned' }">{{ 'dashboard.viewAll' | transloco }}</a>
              </div>
              @if (d.unassignedQueue.length) {
                <ul class="queue">
                  @for (t of d.unassignedQueue; track t.id) {
                    <li class="crm-edge" [class]="'crm-edge-' + t.priority">
                      <a [routerLink]="['/tickets', t.id]">
                        <span class="crm-num crm-muted">{{ t.referenceNumber }}</span>
                        <span class="q-subject" dir="auto">{{ t.subject }}</span>
                        <crm-priority [priority]="t.priority" />
                      </a>
                      <button mat-stroked-button type="button" [disabled]="taking() === t.id" (click)="take(t)">
                        {{ 'dashboard.take' | transloco }}
                      </button>
                    </li>
                  }
                </ul>
              } @else {
                <p class="crm-empty">{{ 'dashboard.unassignedEmpty' | transloco }}</p>
              }
            </section>

            @if (d.team; as team) {
              <section class="crm-panel team">
                <div class="panel-head"><h2>{{ 'dashboard.team' | transloco }}</h2></div>
                <h3 class="crm-muted">{{ 'dashboard.byPriority' | transloco }}</h3>
                <ul class="priorities">
                  @for (p of priorities; track p) {
                    <li><crm-priority [priority]="p" /><b class="crm-num">{{ team.byPriority[p] }}</b></li>
                  }
                </ul>
                <h3 class="crm-muted">{{ 'dashboard.byAgent' | transloco }}</h3>
                <ul class="agents">
                  @for (a of team.openByAgent; track a.agentId) {
                    <li>
                      <span>{{ a.fullName }}</span>
                      <span class="meter"><span [style.inline-size.%]="(a.openTickets / maxAgentLoad()) * 100"></span></span>
                      <b class="crm-num">{{ a.openTickets }}</b>
                    </li>
                  }
                </ul>
              </section>
            }
          </aside>
        </div>
      }
    </div>
  `,
  styles: `
    .intro { margin-block-end: 20px; }
    .intro p { margin: 4px 0 0; }

    .status { margin-block-end: 20px; }
    .bar { display: flex; height: 10px; border-radius: 999px; overflow: hidden; background: var(--crm-rule); gap: 2px; }
    .bar span { flex-basis: 0; min-inline-size: 0; }
    .legend { display: flex; flex-wrap: wrap; gap: 6px 20px; list-style: none; padding: 0; margin: 10px 0 0; font-size: var(--crm-text-s); }
    .legend a { color: inherit; text-decoration: none; display: inline-flex; align-items: center; gap: 6px; }
    .legend a:hover { color: var(--crm-brand); }
    .legend i { width: 10px; height: 10px; border-radius: 2px; display: inline-block; }
    .legend .today { margin-inline-start: auto; }
    [data-status='New'] > span, span[data-status='New'], [data-status='New'] i { background: var(--crm-brand); }
    span[data-status='Open'], [data-status='Open'] i { background: #3d7cc4; }
    span[data-status='InProgress'], [data-status='InProgress'] i { background: #d08a2c; }
    span[data-status='PendingCustomer'], [data-status='PendingCustomer'] i { background: #a58bc9; }
    span[data-status='Resolved'], [data-status='Resolved'] i { background: #3f9a67; }
    span[data-status='Closed'], [data-status='Closed'] i { background: #b5b0c2; }

    .grid { display: grid; grid-template-columns: minmax(0, 1fr) 360px; gap: 20px; align-items: start; }
    .main, .side { display: flex; flex-direction: column; gap: 20px; }
    .panel-head { display: flex; align-items: center; justify-content: space-between; padding: 12px 14px; border-block-end: 1px solid var(--crm-rule); }
    .panel-head h2 { font-size: var(--crm-text-l); display: flex; align-items: center; gap: 8px; }
    .count { font-size: var(--crm-text-s); font-weight: 500; padding: 0 8px; border-radius: 999px; background: var(--crm-brand-soft); color: var(--crm-brand); }

    .queue { list-style: none; margin: 0; padding: 0; }
    .queue li { display: flex; align-items: center; gap: 8px; padding: 8px 12px; border-block-end: 1px solid var(--crm-rule); }
    .queue li:last-child { border-block-end: 0; }
    .queue a { flex: 1; min-width: 0; display: grid; gap: 2px; color: inherit; text-decoration: none; font-size: var(--crm-text-s); }
    .q-subject { font-size: var(--crm-text-m); font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

    .team { padding-block-end: 12px; }
    .team h3 { font-size: var(--crm-text-s); font-weight: 500; padding: 12px 14px 4px; }
    .priorities, .agents { list-style: none; margin: 0; padding: 0 14px; }
    .priorities { display: grid; grid-template-columns: 1fr 1fr; gap: 6px 16px; }
    .priorities li { display: flex; justify-content: space-between; }
    .agents li { display: grid; grid-template-columns: minmax(0, 1fr) 90px 28px; gap: 10px; align-items: center; padding-block: 4px; font-size: var(--crm-text-s); }
    .agents b { text-align: end; }
    .meter { height: 6px; background: var(--crm-rule); border-radius: 999px; overflow: hidden; }
    .meter span { display: block; height: 100%; background: var(--crm-brand); }

    @media (max-width: 1100px) { .grid { grid-template-columns: 1fr; } }
  `,
})
export class DashboardPage {
  private readonly api = inject(CrmApi);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);

  protected readonly data = signal<AgentDashboard | null>(null);
  protected readonly taking = signal<string | null>(null);
  protected readonly priorities = TICKET_PRIORITIES;

  protected readonly firstName = computed(() => (this.auth.user()?.fullName ?? '').split(' ')[0]);
  protected readonly statusSegments = computed(() => {
    const counts = this.data()?.countsByStatus;
    return counts ? TICKET_STATUSES.map(status => ({ status, count: counts[status] ?? 0 })).filter(s => s.count > 0) : [];
  });
  protected readonly statusSummary = computed(() => this.statusSegments().map(s => `${s.status}: ${s.count}`).join(', '));
  protected readonly maxAgentLoad = computed(() => Math.max(1, ...(this.data()?.team?.openByAgent.map(a => a.openTickets) ?? [1])));

  constructor() {
    this.load();
  }

  protected take(ticket: TicketSummary): void {
    this.taking.set(ticket.id);
    this.api.take(ticket.id).subscribe({
      next: () => void this.router.navigate(['/tickets', ticket.id]),
      error: () => {
        this.taking.set(null);
        this.load();
      },
    });
  }

  private load(): void {
    this.api.dashboard().subscribe(d => this.data.set(d));
  }
}
