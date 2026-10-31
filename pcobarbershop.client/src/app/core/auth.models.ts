export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface ConfirmEmailRequest {
  email: string;
  code: string;
}

export interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  email: string;
  roles: string[];
}

// Raspunsul la login cand contul are 2FA: urmeaza pasul cu codul din aplicatie
export interface TwoFactorRequiredResponse {
  requiresTwoFactor: true;
  twoFactorToken: string;
}

export interface LoginTwoFactorRequest {
  twoFactorToken: string;
  code: string;
}

export interface CurrentUser {
  id: string;
  email: string;
  roles: string[];
}
