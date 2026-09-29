import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { TicketPriority, TicketStatus } from '../core/api/models';

@Component({
  selector: 'crm-status-chip',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="chip" [attr.data-status]="status()">{{ 'status.' + status() | transloco }}</span>`,
  styles: `
    .chip {
      display: inline-block;
      padding: 1px 8px;
      border-radius: 999px;
      font-size: var(--crm-text-xs);
      font-weight: 500;
      white-space: nowrap;
      background: #eeecf3;
      color: var(--crm-ink-soft);
    }
    [data-status='New'] { background: var(--crm-brand-soft); color: var(--crm-brand); }
    [data-status='Open'] { background: #e7f0fa; color: #1f5596; }
    [data-status='InProgress'] { background: #fdf1e3; color: #8a4a07; }
    [data-status='PendingCustomer'] { background: #f3eefb; color: #6a4c93; }
    [data-status='Resolved'] { background: #e6f4ec; color: #1d6b3f; }
  `,
})
export class StatusChip {
  readonly status = input.required<TicketStatus>();
}

@Component({
  selector: 'crm-priority',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="p" [attr.data-priority]="priority()"><i aria-hidden="true"></i>{{ 'priority.' + priority() | transloco }}</span>`,
  styles: `
    .p { display: inline-flex; align-items: center; gap: 6px; font-size: var(--crm-text-s); white-space: nowrap; }
    i { width: 8px; height: 8px; border-radius: 2px; background: var(--crm-low); }
    [data-priority='Urgent'] { color: var(--crm-urgent); font-weight: 600; }
    [data-priority='Urgent'] i { background: var(--crm-urgent); }
    [data-priority='High'] i { background: var(--crm-high); }
    [data-priority='Medium'] i { background: var(--crm-medium); }
  `,
})
export class PriorityLabel {
  readonly priority = input.required<TicketPriority>();
}
