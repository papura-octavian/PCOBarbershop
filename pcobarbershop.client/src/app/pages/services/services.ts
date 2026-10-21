import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatRadioModule } from '@angular/material/radio';
import { ServiceItem } from '../../core/catalog.models';
import { CatalogService } from '../../core/catalog.service';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';  


@Component({
  selector: 'app-services',
  imports: [DecimalPipe, MatCardModule, MatRadioModule],
  templateUrl: './services.html',
  styleUrl: './services.css',
})
export class Services implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly router = inject(Router);  

  readonly services = signal<ServiceItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly selectedId = signal<number | null>(null);

  // Serviciul ales, calculat din lista si din id-ul selectat
  readonly selected = computed(
    () => this.services().find((s) => s.id === this.selectedId()) ?? null
  );

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

  select(id: number): void {
    this.selectedId.set(id);
  }

  continueToBooking(): void {
    this.router.navigate(['/book'], { queryParams: { serviceId: this.selectedId() } });
  } 

}
