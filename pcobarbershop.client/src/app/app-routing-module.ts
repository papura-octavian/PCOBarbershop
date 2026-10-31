import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth.guards';
import { Account } from './pages/account/account';
import { BarberArea } from './pages/barber-area/barber-area';
import { Home } from './pages/home/home';
import { Login } from './pages/login/login';
import { Register } from './pages/register/register';
import { Services } from './pages/services/services';
import { Book } from './pages/book/book';
import { ConfirmEmail } from './pages/confirm-email/confirm-email';
import { MyAppointments } from './pages/my-appointments/my-appointments';
import { PaymentSuccess } from './pages/payment-success/payment-success';

const routes: Routes = [
  { path: '', component: Home, pathMatch: 'full' },
  { path: 'login', component: Login, canActivate: [guestGuard] },
  { path: 'register', component: Register, canActivate: [guestGuard] },
  { path: 'confirm-email', component: ConfirmEmail, canActivate: [guestGuard] },
  { path: 'account', component: Account, canActivate: [authGuard] },
  { path: 'barber', component: BarberArea, canActivate: [roleGuard('Frizer')] },
  { path: 'services', component: Services },
  { path: 'book', component: Book },
  { path: 'my-appointments', component: MyAppointments, canActivate: [roleGuard('Client')] },
  { path: 'payment/success', component: PaymentSuccess, canActivate: [roleGuard('Client')] },
  { path: '**', redirectTo: '' },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule],
})
export class AppRoutingModule {}
