import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

// Pagini pentru utilizatori logati
export const authGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }
  // Te trimite la login si retine pagina dorita, ca sa te intoarca la ea dupa login
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

// Pagini pentru vizitatori (login, register): daca esti deja logat, te duce acasa
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAuthenticated() ? router.createUrlTree(['/']) : true;
};

// Pagini pentru un anumit rol, de exemplu roleGuard('Frizer')
export const roleGuard =
  (role: string): CanActivateFn =>
  (route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
    }
    return auth.hasRole(role) ? true : router.createUrlTree(['/']);
  };
