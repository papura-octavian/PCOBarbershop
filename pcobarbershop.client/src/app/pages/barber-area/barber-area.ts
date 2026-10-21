import { Component, OnInit, inject, signal } from '@angular/core';
import { WorkingHoursItem } from '../../core/barber.models';
import { BarberService } from '../../core/barber.service';

const DAY_NAMES = ['Duminica', 'Luni', 'Marti', 'Miercuri', 'Joi', 'Vineri', 'Sambata'];

@Component({
  selector: 'app-barber-area',
  templateUrl: './barber-area.html',
  styleUrl: './barber-area.css',
})
export class BarberArea implements OnInit {
  private readonly barberService = inject(BarberService);

  readonly schedule = signal<WorkingHoursItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

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
  }

  dayName(day: number): string {
    return DAY_NAMES[day];
  }

  // "09:00:00" => "09:00"
  time(value: string): string {
    return value.slice(0, 5);
  }
}
