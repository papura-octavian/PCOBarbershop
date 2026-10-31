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

export interface CurrentUser {
  id: string;
  email: string;
  roles: string[];
}
