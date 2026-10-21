import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute } from '@angular/router';
import { Observable, catchError, forkJoin, of, switchMap, tap } from 'rxjs';
import { BarberItem, SlotItem } from '../../core/barber.models';
import { BarberService } from '../../core/barber.service';
import { ServiceItem } from '../../core/catalog.models';
import { CatalogService } from '../../core/catalog.service';

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
  private readonly catalog = inject(CatalogService);
  private readonly barberService = inject(BarberService);

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

  // Limitele selectorului de data
  readonly minDate = this.toIsoDate(new Date());
  readonly maxDate = this.toIsoDate(this.addDays(new Date(), MAX_DAYS_AHEAD));

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

        // Daca vii din pagina Servicii, serviciul ales e deja completat
        const preselected = Number(this.route.snapshot.queryParamMap.get('serviceId'));
        if (services.some((s) => s.id === preselected)) {
          this.form.patchValue({ serviceId: preselected });
        }
      },
      error: () => {
        this.error.set('Nu am putut incarca datele. Incearca din nou.');
        this.loading.set(false);
      },
    });
  }

  selectSlot(slot: SlotItem): void {
    this.selectedSlot.set(slot);
  }

  // Atentie: toISOString() ar da data in UTC si ar putea decala ziua. Folosim data locala.
  private toIsoDate(date: Date): string {
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${date.getFullYear()}-${month}-${day}`;
  }

  private addDays(date: Date, days: number): Date {
    const copy = new Date(date);
    copy.setDate(copy.getDate() + days);
    return copy;
  }
}
