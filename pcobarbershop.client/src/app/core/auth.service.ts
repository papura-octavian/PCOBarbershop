import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import {
  AuthResponse,
  ConfirmEmailRequest,
  CurrentUser,
  LoginRequest,
  RegisterRequest,
} from './auth.models';

const TOKEN_KEY = 'pcobarbershop_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/auth';

  // Tokenul curent. Un "signal" anunta automat paginile cand se schimba.
  private readonly tokenSignal = signal<string | null>(this.readToken());

  // Utilizatorul curent, calculat din token (null daca nu esti logat sau tokenul a expirat)
  readonly currentUser = computed<CurrentUser | null>(() => this.decode(this.tokenSignal()));

  get token(): string | null {
    return this.tokenSignal();
  }

  register(request: RegisterRequest): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/register`, request);
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/login`, request)
      .pipe(tap((response) => this.saveToken(response.token)));
  }

  // Confirma contul cu codul primit pe e-mail; serverul raspunde direct cu tokenul
  confirmEmail(request: ConfirmEmailRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/confirm-email`, request)
      .pipe(tap((response) => this.saveToken(response.token)));
  }

  resendConfirmation(email: string): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/resend-confirmation`, { email });
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    this.tokenSignal.set(null);
  }

  // Verifica de fiecare data tokenul (inclusiv expirarea), nu doar o valoare memorata
  isAuthenticated(): boolean {
    return this.decode(this.tokenSignal()) !== null;
  }

  hasRole(role: string): boolean {
    return this.currentUser()?.roles.includes(role) ?? false;
  }

  private saveToken(token: string): void {
    localStorage.setItem(TOKEN_KEY, token);
    this.tokenSignal.set(token);
  }

  private readToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  // Un JWT are 3 parti separate prin "." iar a doua contine datele (claims), codate in Base64.
  private decode(token: string | null): CurrentUser | null {
    if (!token) {
      return null;
    }

    try {
      const payload = JSON.parse(this.base64UrlDecode(token.split('.')[1]));

      // "exp" e in secunde, Date.now() in milisecunde
      if (payload.exp * 1000 <= Date.now()) {
        return null;
      }

      const roleClaim = payload['role'];
      const roles: string[] = Array.isArray(roleClaim) ? roleClaim : roleClaim ? [roleClaim] : [];

      return { id: payload['sub'], email: payload['email'], roles };
    } catch {
      return null;
    }
  }

  private base64UrlDecode(value: string): string {
    const base64 = value.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    const bytes = Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
    return new TextDecoder().decode(bytes);
  }
}
