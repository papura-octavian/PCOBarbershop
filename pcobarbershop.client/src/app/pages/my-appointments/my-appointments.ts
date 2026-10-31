import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { RouterLink } from '@angular/router';
import { AppointmentItem, statusLabel } from '../../core/booking.models';
import { BookingService } from '../../core/booking.service';
import { formatSalonDate, formatSalonTime } from '../../core/date-utils';

@Component({
  selector: 'app-my-appointments',
  imports: [DecimalPipe, NgTemplateOutlet, RouterLink, MatCardModule, MatButtonModule],
  templateUrl: './my-appointments.html',
  styleUrl: './my-appointments.css',
})
export class MyAppointments implements OnInit {
  private readonly bookingService = inject(BookingService);

  readonly appointments = signal<AppointmentItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

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
    this.load();
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
