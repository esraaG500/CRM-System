import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { CrmApi } from '../../core/api/crm-api';
import { ContactPerson } from '../../core/api/models';
import { formErrors } from '../../core/http/problem-interceptor';

export interface ContactDialogData { customerId: string; contact: ContactPerson | null; }

@Component({
  selector: 'crm-contact-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatCheckboxModule, MatButtonModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'customers.profile.addContact' | transloco }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="crm-dialog-form">
        <mat-form-field>
          <mat-label>{{ 'customers.form.name' | transloco }}</mat-label>
          <input matInput formControlName="name" maxlength="150" />
          @if (errors()['name']; as e) { <mat-error>{{ e[0] }}</mat-error> }
        </mat-form-field>
        <mat-form-field>
          <mat-label>{{ 'customers.profile.jobTitle' | transloco }}</mat-label>
          <input matInput formControlName="jobTitle" maxlength="100" />
        </mat-form-field>
        <div class="crm-form-grid">
          <mat-form-field>
            <mat-label>{{ 'customers.form.email' | transloco }}</mat-label>
            <input matInput type="email" formControlName="email" dir="ltr" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ 'customers.form.phone' | transloco }}</mat-label>
            <input matInput type="tel" formControlName="phone" dir="ltr" />
          </mat-form-field>
        </div>
        <mat-checkbox formControlName="isPrimary">{{ 'customers.profile.makePrimary' | transloco }}</mat-checkbox>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>{{ 'common.cancel' | transloco }}</button>
        <button mat-flat-button type="submit" [disabled]="busy()">{{ 'common.save' | transloco }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `:host { display: block; width: 520px; max-width: 100%; }`,
})
export class ContactDialog {
  private readonly api = inject(CrmApi);
  private readonly ref = inject(MatDialogRef<ContactDialog, ContactPerson>);
  private readonly data = inject<ContactDialogData>(MAT_DIALOG_DATA);
  protected readonly busy = signal(false);
  protected readonly errors = signal<Record<string, string[]>>({});

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.contact?.name ?? '', Validators.required],
    jobTitle: [this.data.contact?.jobTitle ?? ''],
    email: [this.data.contact?.email ?? '', Validators.email],
    phone: [this.data.contact?.phone ?? ''],
    isPrimary: [this.data.contact?.isPrimary ?? false],
  });

  protected save(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const body = { name: v.name.trim(), jobTitle: v.jobTitle || null, email: v.email || null, phone: v.phone || null, isPrimary: v.isPrimary };
    const request = this.data.contact
      ? this.api.updateContact(this.data.customerId, this.data.contact.id, body)
      : this.api.addContact(this.data.customerId, body);

    this.busy.set(true);
    request.subscribe({
      next: contact => this.ref.close(contact),
      error: e => { this.busy.set(false); this.errors.set(formErrors(e)); },
    });
  }
}
