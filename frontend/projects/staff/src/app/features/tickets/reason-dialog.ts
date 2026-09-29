import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';

export interface ReasonDialogData { titleKey: string; promptKey: string; confirmKey: string; }

/** Asks for the reason required by escalation and by closing an unresolved ticket. */
@Component({
  selector: 'crm-reason-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.titleKey | transloco }}</h2>
    <form (ngSubmit)="confirm()">
      <mat-dialog-content class="crm-dialog-form">
        <p>{{ data.promptKey | transloco }}</p>
        <mat-form-field>
          <mat-label>{{ 'tickets.detail.reason' | transloco }}</mat-label>
          <textarea matInput [formControl]="reason" rows="3" maxlength="500" cdkFocusInitial></textarea>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>{{ 'common.cancel' | transloco }}</button>
        <button mat-flat-button type="submit" [disabled]="reason.invalid">{{ data.confirmKey | transloco }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `:host { display: block; width: 480px; max-width: 100%; } p { margin-top: 0; }`,
})
export class ReasonDialog {
  protected readonly data = inject<ReasonDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ReasonDialog, string>);
  protected readonly reason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3)] });

  protected confirm(): void {
    if (this.reason.valid) this.ref.close(this.reason.value.trim());
  }
}
