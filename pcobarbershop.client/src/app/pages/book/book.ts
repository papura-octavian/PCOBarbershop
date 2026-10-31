import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, catchError, forkJoin, of, switchMap, tap } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { BarberItem, SlotItem } from '../../core/barber.models';
import { BarberService } from '../../core/barber.service';
import { AppointmentItem, statusLabel } from '../../core/booking.models';
import { BookingService } from '../../core/booking.service';
import { ServiceItem } from '../../core/catalog.models';
import { CatalogService } from '../../core/catalog.service';
import { addDays, toIsoDate } from '../../core/date-utils';

const MAX_DAYS_AHEAD = 60;   // aceeasi valoare ca pe server

@Component({
  selector: 'app-book',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './book.html',
  styleUrl: './book.css',
})
export class Book implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly catalog = inject(CatalogService);
  private readonly barberService = inject(BarberService);
  private readonly bookingService = inject(BookingService);
  readonly auth = inject(AuthService);

  // Datele de baza (servicii si frizeri)
  readonly services = signal<ServiceItem[]>([]);
  readonly barbers = signal<BarberItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  // Orele libere pentru alegerea curenta
  readonly slots = signal<SlotItem[]>([]);
  readonly slotsLoading = signal(false);
  readonly slotsLoaded = signal(false);
  readonly slotsError = signal<string | null>(null);
  readonly selectedSlot = signal<SlotItem | null>(null);

  // Rezervarea
  readonly booking = signal(false);
  readonly bookingError = signal<string | null>(null);
  readonly confirmed = signal<AppointmentItem | null>(null);

  // Limitele selectorului de data
  readonly minDate = toIsoDate(new Date());
  readonly maxDate = toIsoDate(addDays(new Date(), MAX_DAYS_AHEAD));

  readonly statusLabel = statusLabel;

  readonly form = this.fb.group({
    serviceId: this.fb.control<number | null>(null),
    barberId: this.fb.control<number | null>(null),
    date: this.fb.control<string>(''),
  });

  // Valorile formularului ca signal, ca sa putem calcula rezumatul
  private readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.value });

  readonly summary = computed(() => {
    const slot = this.selectedSlot();
    const { serviceId, barberId, date } = this.formValue();
    const service = this.services().find((s) => s.id === serviceId);
    const barber = this.barbers().find((b) => b.id === barberId);

    if (!slot || !service || !barber || !date) {
      return null;
    }
    return { service, barber, date: date.split('-').reverse().join('.'), time: slot.time };
  });

  constructor() {
    // La orice schimbare in formular: resetam slotul ales si recalculam orele libere.
    // switchMap anuleaza cererea veche daca utilizatorul schimba repede alegerea.
    this.form.valueChanges
      .pipe(
        tap(() => {
          this.selectedSlot.set(null);
          this.slotsError.set(null);
        }),
        switchMap(({ serviceId, barberId, date }): Observable<SlotItem[] | null> => {
          if (!serviceId || !barberId || !date) {
            this.slots.set([]);
            this.slotsLoaded.set(false);
            return of(null);
          }

          this.slotsLoading.set(true);
          return this.barberService.getSlots(barberId, serviceId, date).pipe(
            catchError(() => {
              this.slotsError.set('Nu am putut incarca orele libere. Incearca din nou.');
              this.slots.set([]);
              this.slotsLoaded.set(false);
              return of(null);
            })
          );
        }),
        takeUntilDestroyed()
      )
      .subscribe((result) => {
        this.slotsLoading.set(false);
        if (result) {
          this.slots.set(result);
          this.slotsLoaded.set(true);
        }
      });
  }

  ngOnInit(): void {
    forkJoin({
      services: this.catalog.getServices(),
      barbers: this.barberService.getBarbers(),
    }).subscribe({
      next: ({ services, barbers }) => {
        this.services.set(services);
        this.barbers.set(barbers);
        this.loading.set(false);

        // Completam alegerile din adresa (vii din pagina Servicii sau dupa login)
        const params = this.route.snapshot.queryParamMap;
        const serviceId = Number(params.get('serviceId'));
        const barberId = Number(params.get('barberId'));
        const date = params.get('date');

        this.form.patchValue({
          serviceId: services.some((s) => s.id === serviceId) ? serviceId : null,
          barberId: barbers.some((b) => b.id === barberId) ? barberId : null,
          date: date && /^\d{4}-\d{2}-\d{2}$/.test(date) ? date : '',
        });
      },
      error: () => {
        this.error.set('Nu am putut incarca datele. Incearca din nou.');
        this.loading.set(false);
      },
    });
  }

  selectSlot(slot: SlotItem): void {
    this.bookingError.set(null);
    this.selectedSlot.set(slot);
  }

  reserve(): void {
    const slot = this.selectedSlot();
    const { serviceId, barberId, date } = this.form.getRawValue();

    if (!slot || !serviceId || !barberId) {
      return;
    }

    // Rezervarea cere cont: te trimitem la login si te aducem inapoi cu alegerile completate
    if (!this.auth.isAuthenticated()) {
      const returnUrl = this.router
        .createUrlTree(['/book'], { queryParams: { serviceId, barberId, date } })
        .toString();
      this.router.navigate(['/login'], { queryParams: { returnUrl } });
      return;
    }

    this.booking.set(true);
    this.bookingError.set(null);

    this.bookingService
      .createAppointment({ barberId, serviceId, startUtc: slot.startUtc })
      .subscribe({
        next: (appointment) => {
          this.booking.set(false);
          this.confirmed.set(appointment);
        },
        error: (err) => {
          this.booking.set(false);

          if (err.status === 409) {
            this.bookingError.set('Ora aleasa nu mai este disponibila. Alege alta ora.');
            this.form.updateValueAndValidity();   // reincarca orele libere
          } else if (err.status === 403) {
            this.bookingError.set('Doar conturile de client pot face rezervari.');
          } else {
            this.bookingError.set('Nu am putut face rezervarea. Incearca din nou.');
          }
        },
      });
  }

  newBooking(): void {
    this.confirmed.set(null);
    this.form.reset({ serviceId: null, barberId: null, date: '' });
  }
}
