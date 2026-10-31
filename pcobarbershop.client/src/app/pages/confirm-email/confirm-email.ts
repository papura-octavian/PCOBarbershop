import { Component, OnDestroy, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { safeReturnUrl } from '../../core/auth.guards';

const RESEND_COOLDOWN_SECONDS = 30;

@Component({
  selector: 'app-confirm-email',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  templateUrl: './confirm-email.html',
  styleUrl: './confirm-email.css',
})
export class ConfirmEmail implements OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly email = this.route.snapshot.queryParamMap.get('email') ?? '';

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly info = signal<string | null>(null);
  readonly cooldown = signal(0);
  private timer?: ReturnType<typeof setInterval>;

  readonly form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.info.set(null);

    this.auth.confirmEmail({ email: this.email, code: this.form.getRawValue().code }).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        this.router.navigateByUrl(safeReturnUrl(returnUrl));
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'A aparut o eroare. Incearca din nou.');
      },
    });
  }

  resend(): void {
    this.errorMessage.set(null);
    this.auth.resendConfirmation(this.email).subscribe({
      next: () => {
        this.info.set('Ti-am trimis un cod nou. Verifica si folderul Spam.');
        this.startCooldown();
      },
      error: () => this.errorMessage.set('Nu am putut trimite codul. Incearca din nou.'),
    });
  }

  ngOnDestroy(): void {
    clearInterval(this.timer);
  }

  private startCooldown(): void {
    this.cooldown.set(RESEND_COOLDOWN_SECONDS);
    clearInterval(this.timer);
    this.timer = setInterval(() => {
      this.cooldown.update((value) => value - 1);
      if (this.cooldown() <= 0) {
        clearInterval(this.timer);
      }
    }, 1000);
  }
}
