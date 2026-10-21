export interface CreateAppointmentRequest {
  barberId: number;
  serviceId: number;
  startUtc: string;
}

export interface AppointmentItem {
  id: number;
  barberName: string;
  serviceName: string;
  price: number;
  startUtc: string;
  endUtc: string;
  status: string;   // PendingPayment | Confirmed | Cancelled
}
