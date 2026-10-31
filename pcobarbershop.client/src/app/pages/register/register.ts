import { Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { AuthResponse, TwoFactorRequiredResponse } from '../../core/auth.models';
import { AuthService } from '../../core/auth.service';
import { GoogleSignIn } from '../../core/google-sign-in';

// Verifica la nivel de formular ca cele doua parole sunt identice
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password === confirm ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-register',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    GoogleSignIn,
  ],
  templateUrl: './register.html',
  styleUrl: './register.css',
})
export class Register {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errors = signal<string[]>([]);

  readonly form = this.fb.nonNullable.group(
    {
      firstName: ['', [Validators.required, Validators.maxLength(50)]],
      lastName: ['', [Validators.required, Validators.maxLength(50)]],
      email: ['', [Validators.required, Validators.email]],
      // aceleasi reguli ca pe server: minim 8 caractere, o cifra, o litera mare
      password: [
        '',
        [
          Validators.required,
          Validators.minLength(8),
          Validators.pattern(/\d/),
          Validators.pattern(/[A-Z]/),
        ],
      ],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch }
  );

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errors.set([]);

    const { confirmPassword, ...request } = this.form.getRawValue();

    // Creeaza contul, apoi te trimite sa introduci codul primit pe e-mail
    this.auth.register(request).subscribe({
      next: () => this.router.navigate(['/confirm-email'], { queryParams: { email: request.email } }),
      error: (err) => {
        this.loading.set(false);
        this.errors.set(this.extractErrors(err));
      },
    });
  }

  // Cont creat / gasit prin Google: e deja confirmat, deci intram direct
  onGoogleSignedIn(response: AuthResponse | TwoFactorRequiredResponse): void {
    if ('requiresTwoFactor' in response) {
      this.router.navigate(['/login'], { state: { twoFactorToken: response.twoFactorToken } });
      return;
    }
    this.router.navigateByUrl('/');
  }

  // Serverul poate trimite erorile in doua forme: o lista (de la Identity)
  // sau un dictionar {camp: [mesaje]} (de la validarea automata)
  private extractErrors(err: any): string[] {
    const serverErrors = err?.error?.errors;

    if (Array.isArray(serverErrors)) {
      return serverErrors;
    }
    if (serverErrors && typeof serverErrors === 'object') {
      return Object.values(serverErrors).flat() as string[];
    }
    return ['A aparut o eroare. Incearca din nou.'];
  }
}
