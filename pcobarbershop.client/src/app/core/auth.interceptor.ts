import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// Ruleaza la FIECARE cerere HTTP facuta din aplicatie.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  // La login si register nu atasam tokenul (si nu tratam 401 ca "sesiune expirata")
  const isAuthEndpoint = req.url.includes('/api/auth/login') || req.url.includes('/api/auth/register');
  const token = auth.token;

  // 1. Atasam tokenul in header: Authorization: Bearer <token>
  const request =
    token && !isAuthEndpoint
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  // 2. Daca serverul raspunde 401 pe o cerere cu token, tokenul e expirat sau invalid
  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && token && !isAuthEndpoint) {
        auth.logout();
        router.navigate(['/login']);
      }
      return throwError(() => error);
    })
  );
};
