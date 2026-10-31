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
  paymentExpiresAt: string | null;   // pana cand se poate plati (doar PendingPayment)
}

const STATUS_LABELS: Record<string, string> = {
  PendingPayment: 'In asteptare plata',
  Confirmed: 'Confirmata',
  Cancelled: 'Anulata',
};

export function statusLabel(status: string): string {
  return STATUS_LABELS[status] ?? status;
}
