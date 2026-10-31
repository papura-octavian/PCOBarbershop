import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { safeReturnUrl } from '../../core/auth.guards';
import { AuthResponse, TwoFactorRequiredResponse } from '../../core/auth.models';
import { AuthService } from '../../core/auth.service';
import { GoogleSignIn } from '../../core/google-sign-in';

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    GoogleSignIn,
  ],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  // Daca e setat, contul are 2FA si asteptam codul din Google Authenticator (pasul 2).
  // Poate veni si de pe pagina de inregistrare, dupa "Continua cu Google".
  readonly twoFactorToken = signal<string | null>(history.state?.twoFactorToken ?? null);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  readonly codeForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.minLength(6)]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: (response) => this.onSignedIn(response),
      error: (err) => {
        this.loading.set(false);

        // Contul exista dar e-mailul nu e confirmat: mergem la pagina cu codul
        if (err.status === 403 && err.error?.requiresEmailConfirmation) {
          this.router.navigate(['/confirm-email'], {
            queryParams: {
              email: err.error.email,
              returnUrl: this.route.snapshot.queryParamMap.get('returnUrl'),
            },
          });
          return;
        }

        this.errorMessage.set(
          err.status === 401
            ? (err.error?.message ?? 'E-mail sau parola incorecte.')
            : 'A aparut o eroare. Incearca din nou.'
        );
      },
    });
  }

  // Dupa parola corecta sau dupa Google: fie suntem logati, fie urmeaza codul 2FA
  onSignedIn(response: AuthResponse | TwoFactorRequiredResponse): void {
    this.loading.set(false);
    if ('requiresTwoFactor' in response) {
      this.twoFactorToken.set(response.twoFactorToken);
      return;
    }
    this.goToReturnUrl();
  }

  submitCode(): void {
    const token = this.twoFactorToken();
    if (!token || this.codeForm.invalid) {
      this.codeForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.auth.loginTwoFactor({ twoFactorToken: token, code: this.codeForm.getRawValue().code }).subscribe({
      next: () => this.goToReturnUrl(),
      error: (err) => {
        this.loading.set(false);

        // Tokenul temporar a expirat (5 minute): o luam de la capat
        if (err.status === 401) {
          this.backToPassword();
        }
        this.errorMessage.set(err?.error?.message ?? 'A aparut o eroare. Incearca din nou.');
      },
    });
  }

  backToPassword(): void {
    this.twoFactorToken.set(null);
    this.codeForm.reset();
    this.errorMessage.set(null);
  }

  private goToReturnUrl(): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    this.router.navigateByUrl(safeReturnUrl(returnUrl));
  }
}
