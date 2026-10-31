import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Observable } from 'rxjs';
import { AccountService, TwoFactorSetup, TwoFactorStatus } from '../../core/account.service';
import { AuthService } from '../../core/auth.service';

// Pagina "Securitate": activarea / dezactivarea autentificarii in doi pasi (Google Authenticator)
@Component({
  selector: 'app-account',
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './account.html',
  styleUrl: './account.css',
})
export class Account implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly accountService = inject(AccountService);
  readonly auth = inject(AuthService);

  readonly status = signal<TwoFactorStatus | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  // Pasii activarii: setup (QR afisat) -> codes (coduri de recuperare afisate)
  readonly setup = signal<TwoFactorSetup | null>(null);
  readonly recoveryCodes = signal<string[] | null>(null);
  readonly disabling = signal(false);

  readonly codeForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
  });

  ngOnInit(): void {
    this.loadStatus();
  }

  startSetup(): void {
    this.run(this.accountService.setupTwoFactor(), (setup) => {
      this.setup.set(setup);
      this.codeForm.reset();
    });
  }

  confirmEnable(): void {
    if (this.codeForm.invalid) {
      this.codeForm.markAllAsTouched();
      return;
    }
    this.run(this.accountService.enableTwoFactor(this.codeForm.getRawValue().code), (result) => {
      this.setup.set(null);
      this.recoveryCodes.set(result.recoveryCodes);
      this.loadStatus();
    });
  }

  startDisable(): void {
    this.disabling.set(true);
    this.error.set(null);
    this.codeForm.reset();
  }

  confirmDisable(): void {
    if (this.codeForm.invalid) {
      this.codeForm.markAllAsTouched();
      return;
    }
    this.run(this.accountService.disableTwoFactor(this.codeForm.getRawValue().code), () => {
      this.disabling.set(false);
      this.recoveryCodes.set(null);
      this.loadStatus();
    });
  }

  cancel(): void {
    this.setup.set(null);
    this.disabling.set(false);
    this.error.set(null);
  }

  closeRecoveryCodes(): void {
    this.recoveryCodes.set(null);
  }

  private loadStatus(): void {
    this.accountService.getTwoFactorStatus().subscribe({
      next: (status) => {
        this.status.set(status);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Nu am putut incarca setarile de securitate.');
        this.loading.set(false);
      },
    });
  }

  // Ruleaza o cerere cu indicator de incarcare si afisarea erorii venite de la server
  private run<T>(request: Observable<T>, onSuccess: (value: T) => void): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: (value) => {
        this.busy.set(false);
        onSuccess(value);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(err?.error?.message ?? 'A aparut o eroare. Incearca din nou.');
      },
    });
  }
}
