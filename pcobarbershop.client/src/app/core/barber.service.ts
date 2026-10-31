import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BarberAppointmentItem, BarberItem, SlotItem, WorkingHoursItem } from './barber.models';

@Injectable({ providedIn: 'root' })
export class BarberService {
  private readonly http = inject(HttpClient);

  getBarbers(): Observable<BarberItem[]> {
    return this.http.get<BarberItem[]>('/api/barbers');
  }

  // date = "yyyy-MM-dd"
  getSlots(barberId: number, serviceId: number, date: string): Observable<SlotItem[]> {
    const params = new HttpParams().set('serviceId', serviceId).set('date', date);
    return this.http.get<SlotItem[]>(`/api/barbers/${barberId}/slots`, { params });
  }

  // Nu trimitem niciun id: serverul stie cine esti din token
  getMySchedule(): Observable<WorkingHoursItem[]> {
    return this.http.get<WorkingHoursItem[]>('/api/barbers/me/schedule');
  }

  // Inlocuieste tot programul saptamanal; zilele lipsa devin zile libere
  updateMySchedule(items: WorkingHoursItem[]): Observable<WorkingHoursItem[]> {
    return this.http.put<WorkingHoursItem[]>('/api/barbers/me/schedule', items);
  }

  // from / to = "yyyy-MM-dd", ambele incluse
  getMyAppointments(from: string, to: string): Observable<BarberAppointmentItem[]> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<BarberAppointmentItem[]>('/api/barbers/me/appointments', { params });
  }
}
