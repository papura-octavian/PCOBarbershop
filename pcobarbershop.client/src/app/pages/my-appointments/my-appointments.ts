import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AppointmentItem, statusLabel } from '../../core/booking.models';
import { BookingService } from '../../core/booking.service';
import { formatSalonDate, formatSalonTime } from '../../core/date-utils';
import { PaymentService } from '../../core/payment.service';

@Component({
  selector: 'app-my-appointments',
  imports: [DecimalPipe, NgTemplateOutlet, RouterLink, MatCardModule, MatButtonModule, MatSnackBarModule],
  templateUrl: './my-appointments.html',
  styleUrl: './my-appointments.css',
})
export class MyAppointments implements OnInit {
  private readonly bookingService = inject(BookingService);
  private readonly payments = inject(PaymentService);
  private readonly route = inject(ActivatedRoute);
  private readonly snackBar = inject(MatSnackBar);

  readonly appointments = signal<AppointmentItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly payingId = signal<number | null>(null);

  // Viitoare: cele mai apropiate primele. Trecute: cele mai recente primele.
  readonly upcoming = computed(() =>
    this.appointments()
      .filter((a) => new Date(a.endUtc) > new Date())
      .sort((a, b) => a.startUtc.localeCompare(b.startUtc))
  );
  readonly past = computed(() => this.appointments().filter((a) => new Date(a.endUtc) <= new Date()));

  readonly statusLabel = statusLabel;
  readonly date = formatSalonDate;
  readonly time = formatSalonTime;

  ngOnInit(): void {
    // Stripe ne intoarce aici cu ?payment=cancelled daca clientul a renuntat la plata
    if (this.route.snapshot.queryParamMap.get('payment') === 'cancelled') {
      this.snackBar.open('Plata a fost anulata. Poti incerca din nou cat timp ora e rezervata.', 'OK', {
        duration: 6000,
      });
    }
    this.load();
  }

  // Se poate plati doar cat timp programarea e in asteptare si termenul nu a trecut
  canPay(a: AppointmentItem): boolean {
    return (
      a.status === 'PendingPayment' && !!a.paymentExpiresAt && new Date(a.paymentExpiresAt) > new Date()
    );
  }

  pay(a: AppointmentItem): void {
    this.payingId.set(a.id);
    this.payments.createCheckout(a.id).subscribe({
      next: (url) => this.payments.redirectTo(url),
      error: (err) => {
        this.payingId.set(null);
        this.snackBar.open(err?.error?.message ?? 'Nu am putut porni plata.', 'OK', { duration: 5000 });
        this.load();
      },
    });
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.bookingService.getMyAppointments().subscribe({
      next: (items) => {
        this.appointments.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Nu am putut incarca programarile. Incearca din nou.');
        this.loading.set(false);
      },
    });
  }
}
