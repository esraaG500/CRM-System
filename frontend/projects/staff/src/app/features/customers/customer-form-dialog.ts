import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { CrmApi } from '../../core/api/crm-api';
import { Customer, CustomerType, CustomerUpsert, Language } from '../../core/api/models';
import { formErrors } from '../../core/http/problem-interceptor';
import { LanguageService } from '../../core/i18n/language';
import { LookupsStore } from '../../core/lookups-store';

const E164 = /^\+[1-9][0-9]{6,14}$/;

function emailOrPhone(group: AbstractControl): ValidationErrors | null {
  const { primaryEmail, primaryPhone } = group.value as { primaryEmail: string; primaryPhone: string };
  return primaryEmail?.trim() || primaryPhone?.trim() ? null : { contactRequired: true };
}

@Component({
  selector: 'crm-customer-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatButtonToggleModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ (customer ? 'customers.form.editTitle' : 'customers.form.createTitle') | transloco }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="crm-dialog-form">
        <mat-button-toggle-group formControlName="type" [attr.aria-label]="'customers.form.type' | transloco" class="type">
          <mat-button-toggle value="Company">{{ 'customers.type.Company' | transloco }}</mat-button-toggle>
          <mat-button-toggle value="Individual">{{ 'customers.type.Individual' | transloco }}</mat-button-toggle>
        </mat-button-toggle-group>

        <mat-form-field>
          <mat-label>{{ 'customers.form.name' | transloco }}</mat-label>
          <input matInput formControlName="name" maxlength="200" cdkFocusInitial />
          @if (form.controls.name.hasError('required')) { <mat-error>{{ 'common.required' | transloco }}</mat-error> }
          @if (serverErrors()['name']; as e) { <mat-error>{{ e[0] }}</mat-error> }
        </mat-form-field>

        <div class="crm-form-grid">
          <mat-form-field>
            <mat-label>{{ 'customers.form.email' | transloco }}</mat-label>
            <input matInput type="email" formControlName="primaryEmail" dir="ltr" />
            @if (form.controls.primaryEmail.hasError('email')) { <mat-error>{{ 'customers.form.emailInvalid' | transloco }}</mat-error> }
            @if (serverErrors()['primaryEmail']; as e) { <mat-error>{{ e[0] }}</mat-error> }
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ 'customers.form.phone' | transloco }}</mat-label>
            <input matInput type="tel" formControlName="primaryPhone" dir="ltr" placeholder="+966501234567" />
            @if (!form.controls.primaryPhone.hasError('pattern')) { <mat-hint>{{ 'customers.form.phoneHint' | transloco }}</mat-hint> }
            @if (form.controls.primaryPhone.hasError('pattern')) { <mat-error>{{ 'customers.form.phoneHint' | transloco }}</mat-error> }
            @if (serverErrors()['primaryPhone']; as e) { <mat-error>{{ e[0] }}</mat-error> }
          </mat-form-field>
        </div>
        @if (form.errors?.['contactRequired'] && form.touched) {
          <p class="form-error" role="alert">{{ 'customers.form.contactRequired' | transloco }}</p>
        }

        <div class="crm-form-grid">
          <mat-form-field>
            <mat-label>{{ 'customers.form.language' | transloco }}</mat-label>
            <mat-select formControlName="preferredLanguage">
              <mat-option value="ar">{{ 'customers.language.ar' | transloco }}</mat-option>
              <mat-option value="en">{{ 'customers.language.en' | transloco }}</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ 'customers.form.branch' | transloco }}</mat-label>
            <mat-select formControlName="branchId">
              <mat-option [value]="null">{{ 'common.none' | transloco }}</mat-option>
              @for (b of lookups.lookups()?.branches ?? []; track b.id) {
                <mat-option [value]="b.id">{{ language.name(b) }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>

        <div class="crm-form-grid">
          <mat-form-field>
            <mat-label>{{ 'customers.form.line1' | transloco }}</mat-label>
            <input matInput formControlName="line1" maxlength="200" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ 'customers.form.city' | transloco }}</mat-label>
            <input matInput formControlName="city" maxlength="100" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ 'customers.form.country' | transloco }}</mat-label>
            <input matInput formControlName="country" maxlength="2" placeholder="SA" dir="ltr" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>{{ 'customers.form.erp' | transloco }}</mat-label>
            <input matInput formControlName="erpReference" maxlength="64" dir="ltr" />
          </mat-form-field>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>{{ 'common.cancel' | transloco }}</button>
        <button mat-flat-button type="submit" [disabled]="busy()">
          {{ (customer ? 'common.saveChanges' : 'customers.new') | transloco }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .type { margin-block-end: 16px; }
    :host { display: block; width: 720px; max-width: 100%; }
    .form-error { color: var(--crm-urgent); margin: 0 0 12px; font-size: var(--crm-text-s); }
  `,
})
export class CustomerFormDialog {
  private readonly api = inject(CrmApi);
  private readonly ref = inject(MatDialogRef<CustomerFormDialog, Customer>);
  protected readonly lookups = inject(LookupsStore);
  protected readonly language = inject(LanguageService);
  protected readonly customer = inject<Customer | null>(MAT_DIALOG_DATA, { optional: true });

  protected readonly busy = signal(false);
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  protected readonly form = inject(NonNullableFormBuilder).group({
    type: [this.customer?.type ?? ('Company' as CustomerType), Validators.required],
    name: [this.customer?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    primaryEmail: [this.customer?.primaryEmail ?? '', Validators.email],
    primaryPhone: [this.customer?.primaryPhone ?? '', Validators.pattern(E164)],
    preferredLanguage: [this.customer?.preferredLanguage ?? (this.language.lang() as Language)],
    branchId: [this.customer?.branchId ?? (null as string | null)],
    line1: [this.customer?.address?.line1 ?? ''],
    city: [this.customer?.address?.city ?? ''],
    country: [this.customer?.address?.country ?? ''],
    erpReference: [this.customer?.erpReference ?? ''],
  }, { validators: emailOrPhone });

  protected save(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const body: CustomerUpsert = {
      type: v.type,
      name: v.name.trim(),
      primaryEmail: v.primaryEmail.trim() || null,
      primaryPhone: v.primaryPhone.trim() || null,
      address: v.line1 || v.city || v.country ? { line1: v.line1 || null, city: v.city || null, country: v.country || null } : null,
      preferredLanguage: v.preferredLanguage,
      branchId: v.branchId,
      erpReference: v.erpReference.trim() || null,
      version: this.customer?.version ?? null,
    };

    const request: Observable<Customer> = this.customer
      ? this.api.updateCustomer(this.customer.id, body)
      : this.api.createCustomer(body);

    this.busy.set(true);
    request.subscribe({
      next: customer => this.ref.close(customer),
      error: e => {
        this.busy.set(false);
        this.serverErrors.set(formErrors(e));
        for (const field of Object.keys(this.serverErrors())) {
          this.form.get(field)?.setErrors({ server: true });
        }
      },
    });
  }
}
