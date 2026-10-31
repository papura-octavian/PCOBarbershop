import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { ServiceItem, serviceIcon } from '../../core/catalog.models';
import { CatalogService } from '../../core/catalog.service';

// Descrieri scurte afisate sub numele serviciului
const DESCRIPTIONS: Record<string, string> = {
  'Tuns simplu': 'Tunsoare clasica sau moderna, cu foarfeca si masina, finisata cu styling.',
  'Tuns + Barba': 'Pachetul complet: tunsoare plus conturarea si aranjarea barbii.',
  Barba: 'Conturare, aranjare si ingrijire a barbii, cu prosop cald.',
};

@Component({
  selector: 'app-services',
  imports: [DecimalPipe, RouterLink, MatButtonModule],
  templateUrl: './services.html',
  styleUrl: './services.css',
})
export class Services implements OnInit {
  private readonly catalog = inject(CatalogService);

  readonly services = signal<ServiceItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly serviceIcon = serviceIcon;

  ngOnInit(): void {
    this.catalog.getServices().subscribe({
      next: (items) => {
        this.services.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Nu am putut incarca serviciile. Incearca din nou.');
        this.loading.set(false);
      },
    });
  }

  description(name: string): string {
    return DESCRIPTIONS[name] ?? 'Serviciu realizat de frizerii nostri cu experienta.';
  }
}
