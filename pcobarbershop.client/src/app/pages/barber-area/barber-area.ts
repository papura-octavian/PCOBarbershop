import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { BarberAppointmentItem, WorkingHoursItem } from '../../core/barber.models';
import { BarberService } from '../../core/barber.service';
import { statusLabel } from '../../core/booking.models';
import {
  addDays,
  formatSalonTime,
  salonDateKey,
  startOfWeek,
  toIsoDate,
} from '../../core/date-utils';

const DAY_NAMES = ['Duminica', 'Luni', 'Marti', 'Miercuri', 'Joi', 'Vineri', 'Sambata'];

interface CalendarDay {
  key: string;            // "2026-10-26"
  label: string;          // "Luni"
  date: string;           // "26.10"
  isToday: boolean;
  appointments: BarberAppointmentItem[];
}

@Component({
  selector: 'app-barber-area',
  imports: [DecimalPipe, MatButtonModule],
  templateUrl: './barber-area.html',
  styleUrl: './barber-area.css',
})
export class BarberArea implements OnInit {
  private readonly barberService = inject(BarberService);

  // Programul de lucru
  readonly schedule = signal<WorkingHoursItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  // Calendarul de rezervari (o saptamana, de luni pana duminica)
  readonly weekStart = signal(startOfWeek(new Date()));
  readonly appointments = signal<BarberAppointmentItem[]>([]);
  readonly calendarLoading = signal(true);
  readonly calendarError = signal<string | null>(null);

  readonly days = computed<CalendarDay[]>(() => {
    const today = toIsoDate(new Date());
    const byDay = new Map<string, BarberAppointmentItem[]>();

    for (const a of this.appointments()) {
      const key = salonDateKey(a.startUtc);
      byDay.set(key, [...(byDay.get(key) ?? []), a]);
    }

    return Array.from({ length: 7 }, (_, i) => {
      const day = addDays(this.weekStart(), i);
      const key = toIsoDate(day);
      return {
        key,
        label: DAY_NAMES[day.getDay()],
        date: `${String(day.getDate()).padStart(2, '0')}.${String(day.getMonth() + 1).padStart(2, '0')}`,
        isToday: key === today,
        appointments: byDay.get(key) ?? [],
      };
    });
  });

  readonly weekLabel = computed(() => {
    const days = this.days();
    return `${days[0].date} - ${days[6].date}.${addDays(this.weekStart(), 6).getFullYear()}`;
  });

  readonly statusLabel = statusLabel;
  readonly formatTime = formatSalonTime;

  ngOnInit(): void {
    this.barberService.getMySchedule().subscribe({
      next: (items) => {
        this.schedule.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Nu am putut incarca programul. Incearca din nou.');
        this.loading.set(false);
      },
    });

    this.loadAppointments();
  }

  previousWeek(): void {
    this.weekStart.set(addDays(this.weekStart(), -7));
    this.loadAppointments();
  }

  nextWeek(): void {
    this.weekStart.set(addDays(this.weekStart(), 7));
    this.loadAppointments();
  }

  currentWeek(): void {
    this.weekStart.set(startOfWeek(new Date()));
    this.loadAppointments();
  }

  loadAppointments(): void {
    const from = toIsoDate(this.weekStart());
    const to = toIsoDate(addDays(this.weekStart(), 6));

    this.calendarLoading.set(true);
    this.calendarError.set(null);

    this.barberService.getMyAppointments(from, to).subscribe({
      next: (items) => {
        this.appointments.set(items);
        this.calendarLoading.set(false);
      },
      error: () => {
        this.calendarError.set('Nu am putut incarca rezervarile. Incearca din nou.');
        this.calendarLoading.set(false);
      },
    });
  }

  dayName(day: number): string {
    return DAY_NAMES[day];
  }

  // "09:00:00" => "09:00"
  time(value: string): string {
    return value.slice(0, 5);
  }
}
