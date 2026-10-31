export interface ServiceItem {
  id: number;
  name: string;
  price: number;
  durationMinutes: number;
}

// Iconita (Material Symbols) potrivita pentru un serviciu, dupa nume
export function serviceIcon(name: string): string {
  const lower = name.toLowerCase();
  if (lower.includes('tuns') && lower.includes('barba')) {
    return 'workspace_premium';
  }
  if (lower.includes('barba')) {
    return 'face_retouching_natural';
  }
  return 'content_cut';
}
