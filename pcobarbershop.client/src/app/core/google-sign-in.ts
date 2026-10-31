import { HttpClient } from '@angular/common/http';
import {
  AfterViewInit,
  Component,
  ElementRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { AuthResponse, TwoFactorRequiredResponse } from './auth.models';
import { AuthService } from './auth.service';

// Tipurile minime pentru biblioteca Google Identity Services (incarcata din script extern)
interface GoogleCredentialResponse {
  credential: string;
}

interface GoogleIdentity {
  accounts: {
    id: {
      initialize(config: { client_id: string; callback: (r: GoogleCredentialResponse) => void }): void;
      renderButton(parent: HTMLElement, options: Record<string, unknown>): void;
    };
  };
}

declare global {
  interface Window {
    google?: GoogleIdentity;
  }
}

const GIS_SCRIPT_URL = 'https://accounts.google.com/gsi/client';
let scriptPromise: Promise<void> | null = null;

// Scriptul Google se incarca o singura data, doar cand e nevoie de buton
function loadGoogleScript(): Promise<void> {
  scriptPromise ??= new Promise<void>((resolve, reject) => {
    const script = document.createElement('script');
    script.src = GIS_SCRIPT_URL;
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => {
      scriptPromise = null;
      reject(new Error('Nu s-a putut incarca Google Sign-In.'));
    };
    document.head.appendChild(script);
  });
  return scriptPromise;
}

// Butonul "Continua cu Google". Apare doar daca serverul are configurat un Client ID.
// Google intoarce un ID token, pe care il trimitem serverului spre verificare.
@Component({
  selector: 'app-google-sign-in',
  template: `
    @if (enabled()) {
      <div class="divider"><span>sau</span></div>
    }
    <div #button class="google-button"></div>
    @if (error()) {
      <p class="form-error">{{ error() }}</p>
    }
  `,
  styles: `
    .divider {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin: 1rem 0;
      opacity: 0.6;
    }
    .divider::before,
    .divider::after {
      content: '';
      flex: 1;
      border-top: 1px solid rgba(128, 128, 128, 0.5);
    }
    .google-button {
      display: flex;
      justify-content: center;
    }
    .form-error {
      color: #b3261e;
    }
  `,
})
export class GoogleSignIn implements AfterViewInit {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly button = viewChild.required<ElementRef<HTMLElement>>('button');

  readonly signedIn = output<AuthResponse | TwoFactorRequiredResponse>();

  readonly enabled = signal(false);
  readonly error = signal<string | null>(null);

  ngAfterViewInit(): void {
    this.http.get<{ enabled: boolean; clientId: string }>('/api/auth/google/config').subscribe({
      next: (config) => {
        if (config.enabled) {
          this.render(config.clientId);
        }
      },
      error: () => {},   // fara configuratie: pur si simplu nu afisam butonul
    });
  }

  private async render(clientId: string): Promise<void> {
    try {
      await loadGoogleScript();
    } catch {
      this.error.set('Autentificarea cu Google nu este disponibila momentan.');
      return;
    }

    const google = window.google!;
    google.accounts.id.initialize({
      client_id: clientId,
      callback: (response) => this.onCredential(response.credential),
    });
    google.accounts.id.renderButton(this.button().nativeElement, {
      theme: 'outline',
      size: 'large',
      text: 'continue_with',
      locale: 'ro',
      width: 300,
    });
    this.enabled.set(true);
  }

  private onCredential(idToken: string): void {
    this.error.set(null);
    this.auth.loginWithGoogle(idToken).subscribe({
      next: (response) => this.signedIn.emit(response),
      error: (err) => this.error.set(err?.error?.message ?? 'Autentificarea cu Google a esuat.'),
    });
  }
}
