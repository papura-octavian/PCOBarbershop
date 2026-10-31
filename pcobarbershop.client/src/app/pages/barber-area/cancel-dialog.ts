import { Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface CancelDialogData {
  clientName: string;
  serviceName: string;
  when: string;   // "26.10.2026, 09:00"
}

// Fereastra de confirmare a anularii. Intoarce motivul (poate fi gol) sau undefined daca renunti.
@Component({
  selector: 'app-cancel-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>Anuleaza programarea</h2>
    <mat-dialog-content>
      <p>
        <strong>{{ data.serviceName }}</strong> cu <strong>{{ data.clientName }}</strong>,
        {{ data.when }}.
      </p>
      <p>Clientul va primi automat un e-mail cu anularea.</p>
      <mat-form-field appearance="outline" class="reason">
        <mat-label>Motivul (optional)</mat-label>
        <textarea matInput rows="3" maxlength="500" [formControl]="reason"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Renunta</button>
      <button mat-flat-button type="button" class="danger" (click)="confirm()">Anuleaza programarea</button>
    </mat-dialog-actions>
  `,
  styles: `
    .reason {
      width: 100%;
    }
    .danger {
      --mat-button-filled-container-color: #b3261e;
      --mat-button-filled-label-text-color: #ffffff;
    }
  `,
})
export class CancelDialog {
  readonly data = inject<CancelDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<CancelDialog, string>);

  readonly reason = new FormControl('', { nonNullable: true, validators: Validators.maxLength(500) });

  confirm(): void {
    this.dialogRef.close(this.reason.value.trim());
  }
}
