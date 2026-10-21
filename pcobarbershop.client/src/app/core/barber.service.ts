import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { WorkingHoursItem } from './barber.models';

@Injectable({ providedIn: 'root' })
export class BarberService {
  private readonly http = inject(HttpClient);

  // Nu trimitem niciun id: serverul stie cine esti din token
  // (interceptorul ataseaza tokenul singur)
  getMySchedule(): Observable<WorkingHoursItem[]> {
    return this.http.get<WorkingHoursItem[]>('/api/barbers/me/schedule');
  }
}
