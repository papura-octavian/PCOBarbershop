export interface WorkingHoursItem {
  dayOfWeek: number;   // 0 = duminica, 1 = luni ... 6 = sambata
  startTime: string;   // "09:00:00"
  endTime: string;
}

export interface BarberItem {
  id: number;
  displayName: string;
}

export interface BarberAppointmentItem {
  id: number;
  clientName: string;
  clientEmail: string;
  serviceName: string;
  price: number;
  startUtc: string;
  endUtc: string;
  status: string;
}

export interface SlotItem {
  startUtc: string;   // se trimite la rezervare
  time: string;       // "09:20", ora locala afisata
}
