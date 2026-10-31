import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface TwoFactorStatus {
  enabled: boolean;
  recoveryCodesLeft: number;
}

export interface TwoFactorSetup {
  sharedKey: string;       // cheia de introdus manual in aplicatie
  qrCodeDataUrl: string;   // imaginea QR (data:image/png;base64,...)
}

// Setarile de securitate ale contului logat
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/account/2fa';

  getTwoFactorStatus(): Observable<TwoFactorStatus> {
    return this.http.get<TwoFactorStatus>(this.apiUrl);
  }

  setupTwoFactor(): Observable<TwoFactorSetup> {
    return this.http.post<TwoFactorSetup>(`${this.apiUrl}/setup`, {});
  }

  // Raspunde cu codurile de recuperare
  enableTwoFactor(code: string): Observable<{ recoveryCodes: string[] }> {
    return this.http.post<{ recoveryCodes: string[] }>(`${this.apiUrl}/enable`, { code });
  }

  disableTwoFactor(code: string): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/disable`, { code });
  }
}
