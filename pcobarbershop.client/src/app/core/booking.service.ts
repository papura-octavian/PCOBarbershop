import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppointmentItem, CreateAppointmentRequest } from './booking.models';

@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly http = inject(HttpClient);

  // Clientul e identificat de server din token (interceptorul il ataseaza)
  createAppointment(request: CreateAppointmentRequest): Observable<AppointmentItem> {
    return this.http.post<AppointmentItem>('/api/appointments', request);
  }
}
