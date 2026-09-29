import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoPipe } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged, filter, map, of, startWith, switchMap } from 'rxjs';
import { CrmApi } from '../../core/api/crm-api';
import { CHANNELS, Channel, ContactPerson, CustomerRef, TICKET_PRIORITIES, Ticket, TicketPriority } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth-store';
import { formErrors } from '../../core/http/problem-interceptor';
import { LanguageService } from '../../core/i18n/language';
import { LookupsStore } from '../../core/lookups-store';

export interface CreateTicketDialogData { customer: CustomerRef | null; contacts: ContactPerson[]; }

@Component({
  selector: 'crm-create-ticket-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatAutocompleteModule, MatButtonModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'tickets.form.title' | transloco }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="crm-dialog-form">
        @if (data.customer; as fixed) {
          <p class="fixed-customer"><span class="crm-muted">{{ 'tickets.form.customer' | transloco }}</span> <b>{{ fixed.name }}</b>
            <span class="crm-num crm-muted">{{ fixed.referenceNumber }}</span></p>
        } @else {
          <mat-form-field>
            <mat-label>{{ 'tickets.form.customer' | transloco }}</mat-label>
            <input matInput [formControl]="customerSearch" [matAutocomplete]="auto" [placeholder]="'tickets.form.customerSearch' | transloco" />
            <mat-autocomplete #auto="matAutocomplete" [displayWith]="displayCustomer" (optionSelected)="pickCustomer($event.option.value)">
              @for (c of customerOptions(); track c.id) {
                <mat-option [value]="c"><span>{{ c.name }}</span> <span class="crm-muted crm-num">{{ c.referenceNumber }}</span></mat-option>
              }
            </mat-autocomplete>
            @if (customerSearch.hasError('required')) { <mat-error>{{ 'tickets.form.customerRequired' | transloco }}</mat-error> }
            @if (errors()['customerId']; as e) { <mat-error>{{ e[0] }}</mat-error> }
          </mat-form-field>
        }

        @if (contacts().length) {
          <mat-form-field>
            <mat-label>{{ 'tickets.form.contact' | transloco }}</mat-label>
            <mat-select formControlName="contactPersonId">
              <mat-option [value]="null">{{ 'common.none' | transloco }}</mat-option>
              @for (p of contacts(); track p.id) { <mat-option [value]="p.id">{{ p.name }}</mat-option> }
            </mat-select>
          </mat-form-field>
        }

        <mat-form-field>
          <mat-label>{{ 'tickets.form.subject' | transloco }}</mat-label>
          <input matInput formControlName="subject" maxlength="250" />
          @if (form.controls.subject.hasError('required')) { <mat-error>{{ 'common.required' | transloco }}</mat-error> }
          @if (errors()['subject']; as e) { <mat-error>{{ e[0] }}</mat-error> }
        </mat-form-field>
        <mat-form-field>
          <mat-label>{{ 'tickets.form.description' | transloco }}</mat-label>
          <textarea matInput formControlName="description" rows="4"></textarea>
          @if (form.controls.description.hasError('required')) { <mat-error>{{ 'common.required' | transloco }}</mat-error> }
        </mat-form-field>

        <div class="crm-form-grid">
          <mat-form-field>
            <mat-label>{{ 'tickets.form.category' | transloco }}</mat-label>
            <mat-select formControlName="categoryId">
              @for (c of lookups.lookups()?.categories ?? []; track c.id) { <mat-option [value]="c.id">{{ language.name(c) }}</mat-option> }
            </mat-select>
            @if (form.controls.categoryId.hasError('required')) { <mat-error>{{ 'common.required' | transloco }}</mat-error> }
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ 'tickets.form.priority' | transloco }}</mat-label>
            <mat-select formControlName="priority">
              @for (p of priorities; track p) { <mat-option [value]="p">{{ 'priority.' + p | transloco }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ 'tickets.form.channel' | transloco }}</mat-label>
            <mat-select formControlName="channel">
              @for (c of channels; track c) { <mat-option [value]="c">{{ 'channel.' + c | transloco }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ 'tickets.form.department' | transloco }}</mat-label>
            <mat-select formControlName="departmentId">
              <mat-option [value]="null">{{ 'tickets.form.departmentHint' | transloco }}</mat-option>
              @for (d of lookups.lookups()?.departments ?? []; track d.id) { <mat-option [value]="d.id">{{ language.name(d) }}</mat-option> }
            </mat-select>
          </mat-form-field>
          @if (canAssign) {
            <mat-form-field>
              <mat-label>{{ 'tickets.form.assignee' | transloco }}</mat-label>
              <mat-select formControlName="assigneeId">
                <mat-option [value]="null">{{ 'common.unassigned' | transloco }}</mat-option>
                @for (a of assignableAgents(); track a.id) { <mat-option [value]="a.id">{{ a.fullName }}</mat-option> }
              </mat-select>
              @if (errors()['assigneeId']; as e) { <mat-error>{{ e[0] }}</mat-error> }
            </mat-form-field>
          }
        </div>
        @if (errors()['assigneeId'] || errors()['departmentId'] || errors()['contactPersonId']; as e) {
          <p class="form-error" role="alert">{{ e[0] }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>{{ 'common.cancel' | transloco }}</button>
        <button mat-flat-button type="submit" [disabled]="busy()">{{ 'tickets.form.create' | transloco }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    :host { display: block; width: 760px; max-width: 100%; }
    .fixed-customer { display: flex; gap: 8px; align-items: baseline; margin: 0 0 16px; }
    .form-error { color: var(--crm-urgent); margin: 0; font-size: var(--crm-text-s); }
  `,
})
export class CreateTicketDialog {
  private readonly api = inject(CrmApi);
  private readonly ref = inject(MatDialogRef<CreateTicketDialog, Ticket>);
  protected readonly data = inject<CreateTicketDialogData>(MAT_DIALOG_DATA, { optional: true }) ?? { customer: null, contacts: [] };
  protected readonly lookups = inject(LookupsStore);
  protected readonly language = inject(LanguageService);
  protected readonly canAssign = inject(AuthStore).can('tickets.assign');
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly channels = CHANNELS;

  protected readonly busy = signal(false);
  protected readonly errors = signal<Record<string, string[]>>({});
  protected readonly customerId = signal<string | null>(this.data.customer?.id ?? null);
  protected readonly contacts = signal<ContactPerson[]>(this.data.contacts);

  protected readonly customerSearch = new FormControl<string | CustomerRef>('', { nonNullable: true });
  protected readonly customerOptions = toSignal(
    this.customerSearch.valueChanges.pipe(
      startWith(''),
      filter((v): v is string => typeof v === 'string'),
      debounceTime(250),
      distinctUntilChanged(),
      switchMap(q => (q.trim().length < 2 ? of([]) : this.api.searchCustomers(q.trim(), 1, 10).pipe(map(r => r.items)))),
    ),
    { initialValue: [] },
  );

  protected readonly form = inject(NonNullableFormBuilder).group({
    contactPersonId: [null as string | null],
    subject: ['', [Validators.required, Validators.maxLength(250)]],
    description: ['', Validators.required],
    categoryId: ['', Validators.required],
    priority: ['Medium' as TicketPriority],
    channel: ['Phone' as Channel],
    departmentId: [null as string | null],
    assigneeId: [null as string | null],
  });

  private readonly departmentValue = toSignal(this.form.controls.departmentId.valueChanges, { initialValue: null });
  private readonly categoryValue = toSignal(this.form.controls.categoryId.valueChanges, { initialValue: '' });

  /** Agents who belong to the department the ticket will be routed to. */
  protected readonly assignableAgents = computed(() => {
    const l = this.lookups.lookups();
    if (!l) return [];
    const dept = this.departmentValue() ?? l.categories.find(c => c.id === this.categoryValue())?.departmentId ?? null;
    return dept ? l.agents.filter(a => a.departmentIds.includes(dept)) : l.agents;
  });

  protected displayCustomer = (c: CustomerRef | string | null): string => (typeof c === 'string' ? c : c?.name ?? '');

  constructor() {
    // Typing after a pick means the agent is searching again: the previous selection no longer applies.
    if (!this.data.customer) {
      this.customerSearch.valueChanges.pipe(takeUntilDestroyed()).subscribe(v => {
        if (typeof v === 'string') {
          this.customerId.set(null);
          this.contacts.set([]);
        }
      });
    }
  }

  protected pickCustomer(c: CustomerRef): void {
    this.customerId.set(c.id);
    this.customerSearch.setErrors(null);
    this.form.controls.contactPersonId.setValue(null);
    this.api.getCustomer(c.id).subscribe(full => this.contacts.set(full.contacts));
  }

  protected save(): void {
    const customerId = this.customerId();
    if (!customerId) {
      this.customerSearch.setErrors({ required: true });
      this.customerSearch.markAsTouched();
    }
    if (this.form.invalid || !customerId || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    this.busy.set(true);
    this.api.createTicket({ ...v, subject: v.subject.trim(), description: v.description.trim(), customerId }).subscribe({
      next: ticket => this.ref.close(ticket),
      error: e => { this.busy.set(false); this.errors.set(formErrors(e)); },
    });
  }
}
