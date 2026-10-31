import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { AppointmentItem } from './booking.models';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private readonly http = inject(HttpClient);

  // Cere serverului o sesiune Stripe Checkout si intoarce adresa paginii de plata
  createCheckout(appointmentId: number): Observable<string> {
    return this.http
      .post<{ url: string }>(`/api/payments/checkout/${appointmentId}`, {})
      .pipe(map((response) => response.url));
  }

  // Dupa intoarcerea de la Stripe: serverul verifica plata direct la Stripe
  confirm(sessionId: string): Observable<AppointmentItem> {
    return this.http.post<AppointmentItem>('/api/payments/confirm', { sessionId });
  }

  // Paraseste aplicatia si deschide pagina de plata securizata Stripe
  redirectTo(url: string): void {
    window.location.href = url;
  }
}
