// Fusul orar al salonului (acelasi ca pe server). Orele vin de la server in UTC
// si le afisam mereu in ora salonului, indiferent de fusul calculatorului.
const SALON_ZONE = 'Europe/Bucharest';

const dateFormat = new Intl.DateTimeFormat('ro-RO', {
  timeZone: SALON_ZONE,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

const timeFormat = new Intl.DateTimeFormat('ro-RO', {
  timeZone: SALON_ZONE,
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

// "en-CA" scrie data ca yyyy-MM-dd
const keyFormat = new Intl.DateTimeFormat('en-CA', {
  timeZone: SALON_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

// "2026-10-26T07:00:00Z" => "26.10.2026"
export function formatSalonDate(utc: string): string {
  return dateFormat.format(new Date(utc));
}

// "2026-10-26T07:00:00Z" => "09:00"
export function formatSalonTime(utc: string): string {
  return timeFormat.format(new Date(utc));
}

// "2026-10-26T07:00:00Z" => "2026-10-26" (ziua din calendarul salonului)
export function salonDateKey(utc: string): string {
  return keyFormat.format(new Date(utc));
}

// Atentie: toISOString() ar da data in UTC si ar putea decala ziua. Folosim data locala.
export function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function addDays(date: Date, days: number): Date {
  const copy = new Date(date);
  copy.setDate(copy.getDate() + days);
  return copy;
}

// Lunea saptamanii din care face parte data
export function startOfWeek(date: Date): Date {
  const copy = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const offset = (copy.getDay() + 6) % 7;   // luni = 0 ... duminica = 6
  return addDays(copy, -offset);
}
