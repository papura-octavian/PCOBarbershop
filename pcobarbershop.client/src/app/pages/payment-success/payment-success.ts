import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AppointmentItem, statusLabel } from '../../core/booking.models';
import { formatSalonDate, formatSalonTime } from '../../core/date-utils';
import { PaymentService } from '../../core/payment.service';

// Pagina pe care Stripe intoarce clientul dupa plata (?session_id=...)
@Component({
  selector: 'app-payment-success',
  imports: [DecimalPipe, RouterLink, MatCardModule, MatButtonModule],
  templateUrl: './payment-success.html',
  styleUrl: './payment-success.css',
})
export class PaymentSuccess implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly payments = inject(PaymentService);

  readonly appointment = signal<AppointmentItem | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly statusLabel = statusLabel;
  readonly date = formatSalonDate;
  readonly time = formatSalonTime;

  ngOnInit(): void {
    const sessionId = this.route.snapshot.queryParamMap.get('session_id');
    if (!sessionId) {
      this.error.set('Lipseste identificatorul platii.');
      this.loading.set(false);
      return;
    }

    this.payments.confirm(sessionId).subscribe({
      next: (appointment) => {
        this.appointment.set(appointment);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? 'Nu am putut verifica plata.');
        this.loading.set(false);
      },
    });
  }
}
