import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { BarberAppointmentItem, WorkingHoursItem } from '../../core/barber.models';
import { BarberService } from '../../core/barber.service';
import { statusLabel } from '../../core/booking.models';
import {
  addDays,
  formatSalonDate,
  formatSalonTime,
  salonDateKey,
  startOfWeek,
  toIsoDate,
} from '../../core/date-utils';
import { CancelDialog, CancelDialogData } from './cancel-dialog';

const DAY_NAMES = ['Duminica', 'Luni', 'Marti', 'Miercuri', 'Joi', 'Vineri', 'Sambata'];

// Ordinea zilelor in formular: luni primul, duminica ultima
const WEEK_ORDER = [1, 2, 3, 4, 5, 6, 0];

interface CalendarDay {
  key: string;            // "2026-10-26"
  label: string;          // "Luni"
  date: string;           // "26.10"
  isToday: boolean;
  appointments: BarberAppointmentItem[];
}

@Component({
  selector: 'app-barber-area',
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatSnackBarModule,
  ],
  templateUrl: './barber-area.html',
  styleUrl: './barber-area.css',
})
export class BarberArea implements OnInit {
  private readonly barberService = inject(BarberService);
  private readonly fb = inject(FormBuilder);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  // Programul de lucru, editabil: un rand pentru fiecare zi a saptamanii
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly saved = signal(false);

  readonly scheduleForm = this.fb.nonNullable.group({
    days: this.fb.array(
      WEEK_ORDER.map((day) =>
        this.fb.nonNullable.group({
          dayOfWeek: day,
          working: false,
          start: '09:00',
          end: '17:00',
        })
      )
    ),
  });

  get dayRows(): FormArray {
    return this.scheduleForm.controls.days;
  }

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
        this.fillSchedule(items);
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

  // Doar programarile viitoare si neanulate pot fi anulate
  canCancel(a: BarberAppointmentItem): boolean {
    return a.status !== 'Cancelled' && new Date(a.startUtc) > new Date();
  }

  cancel(a: BarberAppointmentItem): void {
    const data: CancelDialogData = {
      clientName: a.clientName,
      serviceName: a.serviceName,
      when: `${formatSalonDate(a.startUtc)}, ${formatSalonTime(a.startUtc)}`,
    };

    this.dialog
      .open<CancelDialog, CancelDialogData, string>(CancelDialog, { data, width: '440px' })
      .afterClosed()
      .subscribe((reason) => {
        if (reason === undefined) {
          return;   // a renuntat
        }

        this.barberService.cancelAppointment(a.id, reason).subscribe({
          next: (updated) => {
            this.appointments.update((items) => items.map((i) => (i.id === updated.id ? updated : i)));
            this.snackBar.open('Programarea a fost anulata. Clientul a fost anuntat pe e-mail.', 'OK', {
              duration: 5000,
            });
          },
          error: (err) => {
            this.snackBar.open(err?.error?.message ?? 'Nu am putut anula programarea.', 'OK', {
              duration: 5000,
            });
          },
        });
      });
  }

  saveSchedule(): void {
    const rows = this.scheduleForm.getRawValue().days;
    const working = rows.filter((r) => r.working);

    if (working.some((r) => !r.start || !r.end || r.start >= r.end)) {
      this.saveError.set('La fiecare zi lucratoare, ora de inceput trebuie sa fie inaintea orei de sfarsit.');
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);
    this.saved.set(false);

    const items: WorkingHoursItem[] = working.map((r) => ({
      dayOfWeek: r.dayOfWeek,
      startTime: `${r.start}:00`,
      endTime: `${r.end}:00`,
    }));

    this.barberService.updateMySchedule(items).subscribe({
      next: (result) => {
        this.fillSchedule(result);
        this.saving.set(false);
        this.saved.set(true);
      },
      error: (err) => {
        this.saving.set(false);
        this.saveError.set(err?.error?.message ?? 'Nu am putut salva programul. Incearca din nou.');
      },
    });
  }

  dayName(day: number): string {
    return DAY_NAMES[day];
  }

  // Pune programul primit de la server in formular
  private fillSchedule(items: WorkingHoursItem[]): void {
    for (const row of this.dayRows.controls) {
      const item = items.find((i) => i.dayOfWeek === row.value.dayOfWeek);
      row.patchValue(
        item
          ? { working: true, start: this.time(item.startTime), end: this.time(item.endTime) }
          : { working: false }
      );
    }
    this.scheduleForm.markAsPristine();
  }

  // "09:00:00" => "09:00"
  private time(value: string): string {
    return value.slice(0, 5);
  }
}
