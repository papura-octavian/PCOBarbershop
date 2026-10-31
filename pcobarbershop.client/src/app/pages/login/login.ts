import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { safeReturnUrl } from '../../core/auth.guards';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
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

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        this.router.navigateByUrl(safeReturnUrl(returnUrl));
      },
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
}
