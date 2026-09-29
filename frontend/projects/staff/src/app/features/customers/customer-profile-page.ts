import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { CrmApi } from '../../core/api/crm-api';
import { ContactPerson, Customer, Ticket, TicketSummary, TimelineItem } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth-store';
import { Notifier } from '../../core/http/notifier';
import { LanguageService } from '../../core/i18n/language';
import { AgoPipe, DatePipe } from '../../shared/date-pipes';
import { PriorityLabel, StatusChip } from '../../shared/ticket-badges';
import { CreateTicketDialog, CreateTicketDialogData } from '../tickets/create-ticket-dialog';
import { ContactDialog, ContactDialogData } from './contact-dialog';
import { BackButton } from '../../shared/back-button';
import { CustomerFormDialog } from './customer-form-dialog';

@Component({
  selector: 'crm-customer-profile-page',
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatProgressBarModule,
    TranslocoPipe, AgoPipe, DatePipe, StatusChip, PriorityLabel, BackButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="crm-page">
      @if (!customer()) { <mat-progress-bar mode="indeterminate" /> }
      @if (customer(); as c) {
        <header class="head">
          <crm-back-button fallback="/customers" />
          <div class="who">
            <p class="crm-muted ref"><span class="crm-num">{{ c.referenceNumber }}</span><span>{{ 'customers.type.' + c.type | transloco }}</span></p>
            <h1>{{ c.name }}</h1>
            <p class="crm-muted">{{ 'customers.profile.since' | transloco: { date: (c.createdAt | crmDate: false) } }}</p>
          </div>
          <div class="actions">
            @if (canManage) {
              <button mat-stroked-button type="button" (click)="edit(c)">
                <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>{{ 'common.edit' | transloco }}
              </button>
            }
            <button mat-flat-button type="button" (click)="newTicket(c)">
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon>{{ 'customers.profile.newTicket' | transloco }}
            </button>
          </div>
        </header>

        <div class="layout">
          <aside class="facts">
            <section class="crm-panel block">
              <dl>
                <dt>{{ 'customers.form.email' | transloco }}</dt>
                <dd dir="ltr">{{ c.primaryEmail || '—' }}</dd>
                <dt>{{ 'customers.form.phone' | transloco }}</dt>
                <dd dir="ltr" class="crm-num">{{ c.primaryPhone || '—' }}</dd>
                <dt>{{ 'customers.form.language' | transloco }}</dt>
                <dd>{{ 'customers.language.' + c.preferredLanguage | transloco }}</dd>
                @if (c.address) {
                  <dt>{{ 'customers.form.line1' | transloco }}</dt>
                  <dd>{{ join([c.address.line1, c.address.city, c.address.country], ', ') }}</dd>
                }
                @if (c.erpReference) {
                  <dt>{{ 'customers.form.erp' | transloco }}</dt>
                  <dd class="crm-num" dir="ltr">{{ c.erpReference }}</dd>
                }
              </dl>
            </section>

            @if (c.type === 'Company') {
              <section class="crm-panel">
                <div class="panel-head">
                  <h2>{{ 'customers.profile.contacts' | transloco }}</h2>
                  @if (canManage) {
                    <button mat-button type="button" (click)="editContact(c, null)">{{ 'customers.profile.addContact' | transloco }}</button>
                  }
                </div>
                @if (c.contacts.length) {
                  <ul class="contacts">
                    @for (p of c.contacts; track p.id) {
                      <li>
                        <div>
                          <span class="name">{{ p.name }}</span>
                          @if (p.isPrimary) { <span class="primary">{{ 'customers.profile.primary' | transloco }}</span> }
                          <div class="crm-muted small">{{ p.jobTitle }}</div>
                          <div class="small" dir="ltr">{{ join([p.email, p.phone], '  ') }}</div>
                        </div>
                        @if (canManage) {
                          <span class="row-actions">
                            <button mat-icon-button type="button" (click)="editContact(c, p)" [attr.aria-label]="'common.edit' | transloco">
                              <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
                            </button>
                            <button mat-icon-button type="button" (click)="removeContact(c, p)" [attr.aria-label]="'common.remove' | transloco">
                              <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                            </button>
                          </span>
                        }
                      </li>
                    }
                  </ul>
                } @else {
                  <p class="crm-empty">{{ 'customers.profile.noContacts' | transloco }}</p>
                }
              </section>
            }
          </aside>

          <div class="main">
            <section class="crm-panel">
              <div class="panel-head"><h2>{{ 'customers.profile.tickets' | transloco }}</h2></div>
              @if (tickets().length) {
                <ul class="tickets">
                  @for (t of tickets(); track t.id) {
                    <li class="crm-edge" [class]="'crm-edge-' + t.priority">
                      <a [routerLink]="['/tickets', t.id]">
                        <span class="crm-num crm-muted">{{ t.referenceNumber }}</span>
                        <span class="subject" dir="auto">{{ t.subject }}</span>
                        <crm-priority [priority]="t.priority" />
                        <crm-status-chip [status]="t.status" />
                      </a>
                    </li>
                  }
                </ul>
              } @else {
                <p class="crm-empty">{{ 'tickets.empty' | transloco }}</p>
              }
            </section>

            <section class="crm-panel">
              <div class="panel-head"><h2>{{ 'customers.profile.timeline' | transloco }}</h2></div>
              @if (canManage) {
                <form class="note" (ngSubmit)="addNote(c)">
                  <mat-form-field>
                    <mat-label>{{ 'customers.profile.addNote' | transloco }}</mat-label>
                    <textarea matInput [formControl]="note" rows="2" maxlength="4000" [placeholder]="'customers.profile.notePlaceholder' | transloco"></textarea>
                  </mat-form-field>
                  <button mat-stroked-button type="submit" [disabled]="note.invalid || savingNote()">{{ 'customers.profile.saveNote' | transloco }}</button>
                </form>
              }
              @if (timeline().length) {
                <ol class="timeline">
                  @for (item of timeline(); track $index) {
                    <li [attr.data-kind]="item.kind">
                      <span class="dot" aria-hidden="true"></span>
                      <div>
                        <div class="t-head">
                          @if (item.ticketId) {
                            @if (item.kind === 'Message') {
                              <a [routerLink]="['/tickets', item.ticketId]">{{ 'timeline.' + item.title | transloco }}</a>
                            } @else {
                              <a [routerLink]="['/tickets', item.ticketId]" dir="auto">{{ item.title }}</a>
                            }
                          } @else {
                            <b>{{ 'timeline.' + (item.kind === 'Message' ? item.title : item.kind) | transloco }}</b>
                          }
                          <span class="crm-muted small">{{ item.actor?.fullName }}</span>
                          <span class="crm-muted small">{{ item.occurredAt | crmAgo }}</span>
                        </div>
                        <p dir="auto">{{ item.excerpt }}</p>
                      </div>
                    </li>
                  }
                </ol>
                @if (hasMore()) {
                  <button mat-button type="button" class="more" (click)="loadTimeline(c.id, timelinePage + 1)">{{ 'customers.profile.loadMore' | transloco }}</button>
                }
              } @else {
                <p class="crm-empty">{{ 'customers.profile.noActivity' | transloco }}</p>
              }
            </section>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .head { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; margin-block-end: 20px; }
    .who { flex: 1; min-width: 0; }
    .head p { margin: 0; }
    .ref { font-size: var(--crm-text-s); margin-block-end: 4px !important; display: flex; gap: 12px; }
    .actions { display: flex; gap: 8px; }
    .layout { display: grid; grid-template-columns: 320px minmax(0, 1fr); gap: 20px; align-items: start; }
    .facts, .main { display: flex; flex-direction: column; gap: 20px; }
    .block { padding: 14px; }
    dl { margin: 0; display: grid; grid-template-columns: auto 1fr; gap: 8px 16px; }
    dt { color: var(--crm-ink-soft); font-size: var(--crm-text-s); }
    dd { margin: 0; word-break: break-word; }
    :host-context([dir='rtl']) dd[dir='ltr'] { text-align: right; }
    .panel-head { display: flex; align-items: center; justify-content: space-between; padding: 12px 14px; border-block-end: 1px solid var(--crm-rule); }
    .panel-head h2 { font-size: var(--crm-text-l); }
    .contacts, .tickets, .timeline { list-style: none; margin: 0; padding: 0; }
    .contacts li { display: flex; justify-content: space-between; gap: 8px; padding: 10px 14px; border-block-end: 1px solid var(--crm-rule); }
    .contacts li:last-child { border-block-end: 0; }
    .name { font-weight: 500; }
    .primary { margin-inline-start: 8px; font-size: var(--crm-text-xs); color: var(--crm-brand); background: var(--crm-brand-soft); padding: 0 6px; border-radius: 999px; }
    .small { font-size: var(--crm-text-s); }
    .row-actions { display: flex; }
    .tickets li { border-block-end: 1px solid var(--crm-rule); }
    .tickets li:last-child { border-block-end: 0; }
    .tickets a { display: grid; grid-template-columns: 92px minmax(0, 1fr) 90px auto; gap: 12px; align-items: center; padding: 10px 14px; color: inherit; text-decoration: none; }
    .tickets a:hover { background: var(--crm-brand-soft); }
    .subject { font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .note { display: flex; gap: 12px; align-items: flex-start; padding: 14px 14px 0; }
    .note mat-form-field { flex: 1; }
    .timeline { padding: 8px 14px 14px; }
    .timeline li { display: grid; grid-template-columns: 14px 1fr; gap: 12px; padding-block: 10px; position: relative; }
    .timeline li:not(:last-child)::before {
      content: ''; position: absolute; inset-inline-start: 6px; top: 26px; bottom: -10px; width: 2px; background: var(--crm-rule);
    }
    .dot { width: 14px; height: 14px; border-radius: 50%; border: 3px solid var(--crm-surface); box-shadow: 0 0 0 1px var(--crm-rule); background: var(--crm-low); margin-top: 3px; }
    [data-kind='Ticket'] .dot { background: var(--crm-brand); }
    [data-kind='Note'] .dot { background: #d08a2c; }
    .t-head { display: flex; flex-wrap: wrap; gap: 4px 12px; align-items: baseline; }
    .t-head a { font-weight: 500; text-decoration: none; }
    .timeline p { margin: 2px 0 0; color: var(--crm-ink-soft); white-space: pre-line; }
    .more { margin: 0 14px 14px; }
    @media (max-width: 1000px) { .layout { grid-template-columns: 1fr; } }
  `,
})
export class CustomerProfilePage {
  private readonly api = inject(CrmApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);
  private readonly language = inject(LanguageService);

  readonly customerId = input.required<string>();

  protected readonly canManage = inject(AuthStore).can('customers.manage');
  protected readonly customer = signal<Customer | null>(null);
  protected readonly tickets = signal<TicketSummary[]>([]);
  protected readonly timeline = signal<TimelineItem[]>([]);
  protected readonly hasMore = signal(false);
  protected readonly savingNote = signal(false);
  protected readonly note = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] });
  protected timelinePage = 1;

  constructor() {
    effect(() => this.load(this.customerId()));
  }

  protected edit(c: Customer): void {
    this.dialog.open<CustomerFormDialog, Customer, Customer>(CustomerFormDialog, { data: c, direction: this.language.direction() })
      .afterClosed().subscribe(updated => {
        if (updated) {
          this.customer.set(updated);
          this.notifier.successKey('customers.saved');
        }
      });
  }

  protected editContact(c: Customer, contact: ContactPerson | null): void {
    this.dialog.open<ContactDialog, ContactDialogData, ContactPerson>(ContactDialog, {
      data: { customerId: c.id, contact }, direction: this.language.direction(),
    }).afterClosed().subscribe(saved => {
      if (saved) {
        this.notifier.successKey('customers.profile.contactSaved');
        this.reloadCustomer(c.id);
      }
    });
  }

  protected removeContact(c: Customer, contact: ContactPerson): void {
    this.api.removeContact(c.id, contact.id).subscribe(() => {
      this.notifier.successKey('customers.profile.contactRemoved');
      this.reloadCustomer(c.id);
    });
  }

  protected newTicket(c: Customer): void {
    this.dialog.open<CreateTicketDialog, CreateTicketDialogData, Ticket>(CreateTicketDialog, {
      data: { customer: { id: c.id, name: c.name, referenceNumber: c.referenceNumber }, contacts: c.contacts },
      direction: this.language.direction(),
    }).afterClosed().subscribe(ticket => {
      if (ticket) {
        this.notifier.successKey('tickets.created', { ref: ticket.referenceNumber });
        this.load(c.id);
      }
    });
  }

  protected addNote(c: Customer): void {
    if (this.note.invalid) return;
    this.savingNote.set(true);
    this.api.addNote(c.id, this.note.value.trim()).subscribe({
      next: () => {
        this.note.reset();
        this.savingNote.set(false);
        this.notifier.successKey('customers.profile.noteSaved');
        this.loadTimeline(c.id, 1);
      },
      error: () => this.savingNote.set(false),
    });
  }

  protected loadTimeline(customerId: string, page: number): void {
    this.api.timeline(customerId, page).subscribe(r => {
      this.timelinePage = page;
      this.timeline.set(page === 1 ? r.items : [...this.timeline(), ...r.items]);
      this.hasMore.set(r.page * r.pageSize < r.totalCount);
    });
  }

  private load(customerId: string): void {
    this.reloadCustomer(customerId);
    this.api.listTickets({ customerId, pageSize: 50 }).subscribe(r => this.tickets.set(r.items));
    this.loadTimeline(customerId, 1);
  }

  protected join(parts: (string | null | undefined)[], separator: string): string {
    return parts.filter(p => !!p).join(separator);
  }

  private reloadCustomer(customerId: string): void {
    this.api.getCustomer(customerId).subscribe(c => this.customer.set(c));
  }
}
