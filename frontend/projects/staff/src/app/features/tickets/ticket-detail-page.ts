import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { CrmApi } from '../../core/api/crm-api';
import { Message, TICKET_PRIORITIES, Ticket, TicketHistoryEntry, TicketPriority, TicketStatus } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth-store';
import { Notifier } from '../../core/http/notifier';
import { LanguageService } from '../../core/i18n/language';
import { LookupsStore } from '../../core/lookups-store';
import { AgoPipe, DatePipe } from '../../shared/date-pipes';
import { PriorityLabel, StatusChip } from '../../shared/ticket-badges';
import { BackButton } from '../../shared/back-button';
import { ReasonDialog, ReasonDialogData } from './reason-dialog';

type ComposerMode = 'reply' | 'note';

@Component({
  selector: 'crm-ticket-detail-page',
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatIconModule, MatMenuModule, MatProgressBarModule, MatTabsModule, TranslocoPipe, AgoPipe, DatePipe, PriorityLabel, StatusChip, BackButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="crm-page">
      @if (!ticket()) { <mat-progress-bar mode="indeterminate" /> }
      @if (ticket(); as t) {
        <header class="head">
          <crm-back-button fallback="/tickets" />
          <div class="title crm-edge" [class]="'crm-edge-' + t.priority">
            <p class="meta">
              <span class="crm-num">{{ t.referenceNumber }}</span>
              <crm-status-chip [status]="t.status" />
              @if (t.escalationLevel > 0) {
                <span class="escalated"><mat-icon fontSet="material-symbols-outlined" inline>north</mat-icon>{{ t.escalationLevel }}</span>
              }
            </p>
            <h1 dir="auto">{{ t.subject }}</h1>
          </div>
          @if (canManage) {
            <div class="actions">
              @if (!t.assignee && t.status !== 'Closed') {
                <button mat-stroked-button type="button" (click)="take(t)" [disabled]="busy()">{{ 'tickets.detail.take' | transloco }}</button>
              }
              @if (t.allowedTransitions.length) {
                <button mat-flat-button type="button" [matMenuTriggerFor]="statusMenu" [disabled]="busy()">
                  {{ 'tickets.detail.changeStatus' | transloco }}<mat-icon fontSet="material-symbols-outlined" iconPositionEnd>expand_more</mat-icon>
                </button>
                <mat-menu #statusMenu="matMenu">
                  @for (s of t.allowedTransitions; track s) {
                    <button mat-menu-item type="button" (click)="changeStatus(t, s)">{{ 'status.' + s | transloco }}</button>
                  }
                </mat-menu>
              }
              @if (t.status !== 'Closed') {
                <button mat-button type="button" (click)="escalate(t)" [disabled]="busy()">
                  <mat-icon fontSet="material-symbols-outlined">priority_high</mat-icon>{{ 'tickets.detail.escalate' | transloco }}
                </button>
              }
            </div>
          }
        </header>

        <div class="layout">
          <div class="main">
            <section class="crm-panel description"><p>{{ t.description }}</p></section>

            <mat-tab-group class="crm-panel tabs" animationDuration="0ms" (selectedIndexChange)="onTab($event)">
              <mat-tab [label]="'tickets.detail.conversation' | transloco">
                <ol class="messages">
                  @for (m of messages(); track m.id) {
                    <li [class.internal]="m.isInternal" [class.inbound]="m.direction === 'Inbound'">
                      <div class="m-head">
                        <b>{{ m.sender.name }}</b>
                        @if (m.isInternal) { <span class="tag">{{ 'tickets.detail.internal' | transloco }}</span> }
                        <span class="crm-muted">{{ m.createdAt | crmAgo }}</span>
                        @if (m.deliveryStatus === 'Failed') { <span class="failed">{{ 'tickets.detail.deliveryFailed' | transloco }}</span> }
                      </div>
                      <p dir="auto">{{ m.body }}</p>
                    </li>
                  } @empty {
                    <li class="crm-empty">{{ 'tickets.detail.noMessages' | transloco }}</li>
                  }
                </ol>

                @if (canManage && t.status !== 'Closed') {
                  <form class="composer" [class.note-mode]="mode() === 'note'" (ngSubmit)="send(t, false)">
                    <mat-button-toggle-group class="mode" [value]="mode()" (change)="mode.set($event.value)" hideSingleSelectionIndicator>
                      <mat-button-toggle value="reply">{{ 'tickets.detail.reply' | transloco }}</mat-button-toggle>
                      <mat-button-toggle value="note">{{ 'tickets.detail.note' | transloco }}</mat-button-toggle>
                    </mat-button-toggle-group>
                    <mat-form-field>
                      <textarea matInput [formControl]="body" rows="4" maxlength="20000"
                                [placeholder]="(mode() === 'reply' ? 'tickets.detail.replyPlaceholder' : 'tickets.detail.notePlaceholder') | transloco"></textarea>
                    </mat-form-field>
                    <div class="composer-actions">
                      @if (mode() === 'note') {
                        <mat-form-field class="mentions">
                          <mat-label>{{ 'tickets.detail.mention' | transloco }}</mat-label>
                          <mat-select [formControl]="mentions" multiple>
                            @for (a of lookups.lookups()?.agents ?? []; track a.id) { <mat-option [value]="a.id">{{ a.fullName }}</mat-option> }
                          </mat-select>
                        </mat-form-field>
                        <button mat-flat-button type="submit" [disabled]="body.invalid || busy()">{{ 'tickets.detail.saveNote' | transloco }}</button>
                      } @else {
                        @if (t.allowedTransitions.includes('Resolved')) {
                          <button mat-stroked-button type="button" (click)="send(t, true)" [disabled]="body.invalid || busy()">
                            {{ 'tickets.detail.sendAndResolve' | transloco }}
                          </button>
                        }
                        <button mat-flat-button type="submit" [disabled]="body.invalid || busy()">{{ 'tickets.detail.send' | transloco }}</button>
                      }
                    </div>
                  </form>
                }
              </mat-tab>

              <mat-tab [label]="'tickets.detail.history' | transloco">
                <ol class="history">
                  @for (h of history(); track $index) {
                    <li>
                      <span class="crm-muted when">{{ h.timestamp | crmDate }}</span>
                      <span><b>{{ h.user?.fullName ?? ('tickets.history.system' | transloco) }}</b> {{ describe(h) }}</span>
                      @if (h.reason) { <span class="reason">“{{ h.reason }}”</span> }
                    </li>
                  }
                </ol>
              </mat-tab>
            </mat-tab-group>
          </div>

          <aside class="side">
            <section class="crm-panel">
              <dl>
                <dt>{{ 'tickets.detail.customer' | transloco }}</dt>
                <dd class="customer-ref"><a [routerLink]="['/customers', t.customer.id]">{{ t.customer.name }}</a>
                  <span class="crm-num crm-muted small">{{ t.customer.referenceNumber }}</span></dd>
                @if (t.contactPerson; as p) {
                  <dt>{{ 'tickets.detail.contact' | transloco }}</dt>
                  <dd>{{ p.name }}<div class="small" dir="ltr">{{ p.email ?? p.phone }}</div></dd>
                }

                <dt>{{ 'tickets.detail.assignee' | transloco }}</dt>
                <dd>
                  @if (canAssign && t.status !== 'Closed') {
                    <mat-select class="inline-select" [value]="t.assignee?.id ?? null" (selectionChange)="assign(t, $event.value)"
                                [attr.aria-label]="'tickets.detail.assign' | transloco">
                      <mat-option [value]="null">{{ 'common.unassigned' | transloco }}</mat-option>
                      @for (a of departmentAgents(); track a.id) { <mat-option [value]="a.id">{{ a.fullName }}</mat-option> }
                    </mat-select>
                  } @else {
                    {{ t.assignee?.fullName ?? ('common.unassigned' | transloco) }}
                  }
                </dd>

                <dt>{{ 'tickets.detail.priority' | transloco }}</dt>
                <dd>
                  @if (canManage && t.status !== 'Closed') {
                    <mat-select class="inline-select" [value]="t.priority" (selectionChange)="setPriority(t, $event.value)"
                                [attr.aria-label]="'tickets.detail.priority' | transloco">
                      @for (p of priorities; track p) { <mat-option [value]="p"><crm-priority [priority]="p" /></mat-option> }
                    </mat-select>
                  } @else {
                    <crm-priority [priority]="t.priority" />
                  }
                </dd>

                <dt>{{ 'tickets.detail.category' | transloco }}</dt>
                <dd>{{ language.name(t.category) }}</dd>
                <dt>{{ 'tickets.detail.department' | transloco }}</dt>
                <dd>{{ language.name(t.department) }}</dd>
                <dt>{{ 'tickets.detail.channel' | transloco }}</dt>
                <dd>{{ 'channel.' + t.channel | transloco }}</dd>
                <dt>{{ 'tickets.detail.created' | transloco }}</dt>
                <dd>{{ t.createdAt | crmDate }}</dd>
                <dt>{{ 'tickets.detail.firstResponse' | transloco }}</dt>
                <dd>{{ t.firstRespondedAt ? (t.firstRespondedAt | crmDate) : ('tickets.detail.notYet' | transloco) }}</dd>
              </dl>
            </section>
          </aside>
        </div>
      }
    </div>
  `,
  styles: `
    .head { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; margin-block-end: 20px; }
    .title { flex: 1; min-width: 0; padding-inline-start: 14px; }
    .title h1 { overflow-wrap: anywhere; text-align: left; }
    :host-context([dir='rtl']) .title h1 { text-align: right; }
    .meta { display: flex; align-items: center; gap: 10px; margin: 0 0 6px; color: var(--crm-ink-soft); font-size: var(--crm-text-s); }
    .escalated { display: inline-flex; align-items: center; color: var(--crm-urgent); font-weight: 600; }
    .actions { display: flex; gap: 8px; flex-wrap: wrap; }
    .layout { display: grid; grid-template-columns: minmax(0, 1fr) 320px; gap: 20px; align-items: start; }
    .main { display: flex; flex-direction: column; gap: 16px; min-width: 0; }
    .description p { margin: 0; padding: 14px 16px; white-space: pre-line; }
    .tabs { overflow: hidden; }

    .messages, .history { list-style: none; margin: 0; padding: 8px 16px; }
    .messages li { padding: 12px 14px; margin-block: 8px; border-radius: var(--crm-radius-m); background: var(--crm-mist); max-width: 88%; }
    .messages li.inbound { background: var(--crm-surface); border: 1px solid var(--crm-rule); }
    .messages li:not(.inbound):not(.internal) { margin-inline-start: auto; background: var(--crm-brand-soft); }
    .messages li.internal { background: #fff8e8; border: 1px dashed #e1b35b; margin-inline-start: auto; }
    .messages li.crm-empty { background: none; max-width: none; }
    .m-head { display: flex; gap: 10px; align-items: baseline; font-size: var(--crm-text-s); margin-block-end: 4px; }
    .tag { font-size: var(--crm-text-xs); color: #8a5a00; background: #fbe8c2; border-radius: 999px; padding: 0 6px; }
    .failed { color: var(--crm-urgent); }
    .messages p { margin: 0; white-space: pre-line; }

    .composer { padding: 12px 16px 16px; border-block-start: 1px solid var(--crm-rule); display: flex; flex-direction: column; gap: 10px; }
    .composer.note-mode { background: #fffcf4; }
    .mode { align-self: flex-start; }
    .customer-ref { display: flex; flex-wrap: wrap; gap: 8px; align-items: baseline; }
    .composer-actions { display: flex; gap: 8px; justify-content: flex-end; align-items: center; flex-wrap: wrap; }
    .mentions { flex: 1; max-width: 360px; }

    .history li { display: grid; grid-template-columns: 170px 1fr; gap: 4px 12px; padding-block: 8px; border-block-end: 1px solid var(--crm-rule); }
    .history li:last-child { border-block-end: 0; }
    .when { font-size: var(--crm-text-s); }
    .reason { grid-column: 2; color: var(--crm-ink-soft); font-size: var(--crm-text-s); }

    dl { margin: 0; padding: 14px 16px; display: grid; grid-template-columns: auto 1fr; gap: 10px 16px; align-items: center; }
    dt { color: var(--crm-ink-soft); font-size: var(--crm-text-s); }
    dd { margin: 0; min-width: 0; }
    .small { font-size: var(--crm-text-s); }
    .inline-select { border: 1px solid var(--crm-rule); border-radius: var(--crm-radius-s); padding: 4px 8px; }

    @media (max-width: 1000px) { .layout { grid-template-columns: 1fr; } .side { order: -1; } }
    @media (max-width: 600px) { .history li { grid-template-columns: 1fr; } .reason { grid-column: 1; } }
  `,
})
export class TicketDetailPage {
  private readonly api = inject(CrmApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);
  private readonly i18n = inject(TranslocoService);
  protected readonly language = inject(LanguageService);
  protected readonly lookups = inject(LookupsStore);

  readonly ticketId = input.required<string>();

  private readonly auth = inject(AuthStore);
  protected readonly canManage = this.auth.can('tickets.manage');
  protected readonly canAssign = this.auth.can('tickets.assign');
  protected readonly priorities = TICKET_PRIORITIES;

  protected readonly ticket = signal<Ticket | null>(null);
  protected readonly messages = signal<Message[]>([]);
  protected readonly history = signal<TicketHistoryEntry[]>([]);
  protected readonly busy = signal(false);
  protected readonly mode = signal<ComposerMode>('reply');
  protected readonly body = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(20000)] });
  protected readonly mentions = new FormControl<string[]>([], { nonNullable: true });

  protected readonly departmentAgents = computed(() => {
    const dept = this.ticket()?.department?.id;
    return (this.lookups.lookups()?.agents ?? []).filter(a => !dept || a.departmentIds.includes(dept));
  });

  constructor() {
    effect(() => {
      const id = this.ticketId();
      this.api.getTicket(id).subscribe(t => this.ticket.set(t));
      this.loadMessages(id);
      this.loadHistory(id);
    });
  }

  protected onTab(index: number): void {
    if (index === 1) this.loadHistory(this.ticketId());
  }

  protected take(t: Ticket): void {
    this.run(this.api.take(t.id));
  }

  protected assign(t: Ticket, assigneeId: string | null): void {
    this.run(this.api.assign(t.id, assigneeId, t.version));
  }

  protected setPriority(t: Ticket, priority: TicketPriority): void {
    this.run(this.api.updateTicket(t.id, { priority, version: t.version }));
  }

  protected changeStatus(t: Ticket, status: TicketStatus): void {
    if (status === 'Closed' && t.status !== 'Resolved') {
      this.askReason('tickets.detail.changeStatus', 'tickets.detail.closeReason', 'status.Closed', reason =>
        this.run(this.api.changeStatus(t.id, status, t.version, reason)));
      return;
    }
    this.run(this.api.changeStatus(t.id, status, t.version));
  }

  protected escalate(t: Ticket): void {
    this.askReason('tickets.detail.escalate', 'tickets.detail.escalateReason', 'tickets.detail.escalate', reason =>
      this.run(this.api.escalate(t.id, reason, t.version)));
  }

  protected send(t: Ticket, resolve: boolean): void {
    if (this.body.invalid) return;
    const text = this.body.value.trim();
    const request = this.mode() === 'note'
      ? this.api.addInternalNote(t.id, text, this.mentions.value)
      : this.api.reply(t.id, text, resolve ? 'Resolved' : undefined);

    this.busy.set(true);
    request.subscribe({
      next: () => {
        this.notifier.successKey(this.mode() === 'note' ? 'tickets.detail.noteAdded' : 'tickets.detail.replied');
        this.body.reset();
        this.mentions.reset();
        this.busy.set(false);
        this.refresh();
      },
      error: () => this.busy.set(false),
    });
  }

  protected describe(h: TicketHistoryEntry): string {
    const status = (v: string | null) => (v ? this.i18n.translate(`status.${v}`) : '');
    switch (h.changeType) {
      case 'StatusChanged':
        return this.i18n.translate('tickets.history.StatusChanged', { from: status(h.oldValue), to: status(h.newValue) });
      case 'Assigned':
        return h.newValue
          ? this.i18n.translate('tickets.history.Assigned', { to: this.lookups.agentName(h.newValue) || '—' })
          : this.i18n.translate('tickets.history.Unassigned');
      case 'Escalated':
        return this.i18n.translate('tickets.history.Escalated', { to: h.newValue });
      case 'FieldChanged': {
        const value = (v: string | null) => (h.field === 'Priority' && v ? this.i18n.translate(`priority.${v}`) : this.nameOf(v));
        return this.i18n.translate('tickets.history.FieldChanged', {
          field: this.i18n.translate(`tickets.detail.${h.field === 'CategoryId' ? 'category' : h.field === 'DepartmentId' ? 'department' : 'priority'}`),
          from: value(h.oldValue), to: value(h.newValue),
        });
      }
      default:
        return this.i18n.translate(`tickets.history.${h.changeType}`);
    }
  }

  private nameOf(id: string | null): string {
    const l = this.lookups.lookups();
    const ref = l?.categories.find(c => c.id === id) ?? l?.departments.find(d => d.id === id);
    return ref ? this.language.name(ref) : id ?? '';
  }

  private askReason(titleKey: string, promptKey: string, confirmKey: string, then: (reason: string) => void): void {
    this.dialog.open<ReasonDialog, ReasonDialogData, string>(ReasonDialog, {
      data: { titleKey, promptKey, confirmKey }, direction: this.language.direction(),
    }).afterClosed().subscribe(reason => reason && then(reason));
  }

  private run(request: Observable<Ticket>): void {
    this.busy.set(true);
    request.subscribe({
      next: t => {
        this.ticket.set(t);
        this.busy.set(false);
        this.notifier.successKey('tickets.detail.updated');
        this.loadHistory(t.id);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        // A stale version means someone else changed the ticket: show the latest state.
        if (e instanceof HttpErrorResponse && e.status === 409) this.refresh();
      },
    });
  }

  private refresh(): void {
    const id = this.ticketId();
    this.api.getTicket(id).subscribe(t => this.ticket.set(t));
    this.loadMessages(id);
    this.loadHistory(id);
  }

  private loadMessages(id: string): void {
    this.api.messages(id).subscribe(m => this.messages.set(m));
  }

  private loadHistory(id: string): void {
    this.api.history(id).subscribe(h => this.history.set(h));
  }
}
