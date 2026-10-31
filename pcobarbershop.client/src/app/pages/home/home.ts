import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { BarberItem } from '../../core/barber.models';
import { BarberService } from '../../core/barber.service';
import { ServiceItem, serviceIcon } from '../../core/catalog.models';
import { CatalogService } from '../../core/catalog.service';

@Component({
  selector: 'app-home',
  imports: [DecimalPipe, RouterLink, MatButtonModule],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home implements OnInit {
  readonly auth = inject(AuthService);
  private readonly catalog = inject(CatalogService);
  private readonly barberService = inject(BarberService);

  // Serviciile si frizerii afisati pe prima pagina (daca nu se incarca, sectiunile raman ascunse)
  readonly services = signal<ServiceItem[]>([]);
  readonly barbers = signal<BarberItem[]>([]);

  readonly serviceIcon = serviceIcon;

  ngOnInit(): void {
    forkJoin({
      services: this.catalog.getServices(),
      barbers: this.barberService.getBarbers(),
    }).subscribe({
      next: ({ services, barbers }) => {
        this.services.set(services);
        this.barbers.set(barbers);
      },
      error: () => {},
    });
  }

  initials(name: string): string {
    return name
      .split(' ')
      .map((part) => part.charAt(0))
      .join('')
      .slice(0, 2)
      .toUpperCase();
  }
}
