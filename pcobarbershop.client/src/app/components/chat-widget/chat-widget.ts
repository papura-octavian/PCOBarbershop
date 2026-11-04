import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, effect, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AssistantMessage } from '../../core/assistant.models';
import { AssistantService } from '../../core/assistant.service';
import { AuthService } from '../../core/auth.service';
import { PaymentService } from '../../core/payment.service';

const MAX_HISTORY = 20; // same limit as the server
const REDIRECT_DELAY_MS = 1500; // time to read the assistant's answer before leaving for Stripe

@Component({
  selector: 'app-chat-widget',
  imports: [RouterLink],
  templateUrl: './chat-widget.html',
  styleUrl: './chat-widget.css',
})
export class ChatWidget {
  protected readonly auth = inject(AuthService);
  private readonly assistant = inject(AssistantService);
  private readonly payments = inject(PaymentService);

  protected readonly isOpen = signal(false);
  protected readonly messages = signal<AssistantMessage[]>([]);
  protected readonly draft = signal('');
  protected readonly isSending = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly isRedirecting = signal(false);

  protected readonly suggestions = [
    'Cine e liber maine?',
    'Ce servicii aveti si cat costa?',
    'Vreau o tunsoare vineri dupa-amiaza',
  ];

  private readonly scroller = viewChild<ElementRef<HTMLElement>>('scroller');
  private readonly inputBox = viewChild<ElementRef<HTMLTextAreaElement>>('inputBox');

  constructor() {
    // When the logged-in user changes (login/logout), start a fresh conversation:
    // the next user must not see the previous one's messages
    effect(() => {
      this.auth.currentUser();
      this.messages.set([]);
      this.error.set(null);
    });
  }

  toggle(): void {
    this.isOpen.update((open) => !open);
    if (this.isOpen()) {
      this.focusInput();
      this.scrollToBottom();
    }
  }

  reset(): void {
    this.messages.set([]);
    this.error.set(null);
    this.focusInput();
  }

  onInput(event: Event): void {
    this.draft.set((event.target as HTMLTextAreaElement).value);
  }

  // Enter sends, Shift+Enter adds a new line
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  send(text?: string): void {
    const content = (text ?? this.draft()).trim();
    if (!content || this.isSending() || this.isRedirecting()) {
      return;
    }

    this.error.set(null);
    this.messages.update((list) => [...list, { role: 'user', content }]);
    this.draft.set('');
    this.isSending.set(true);
    this.scrollToBottom();

    this.assistant.chat(this.messages().slice(-MAX_HISTORY)).subscribe({
      next: (response) => {
        const answer = response.reply.trim() || 'Nu am putut formula un raspuns. Incearca sa reformulezi.';
        this.messages.update((list) => [...list, { role: 'assistant', content: answer }]);
        this.isSending.set(false);
        this.scrollToBottom();

        // The assistant booked an appointment: go to the card payment, like the booking page does
        if (response.pendingPaymentAppointmentId !== null) {
          this.startPayment(response.pendingPaymentAppointmentId);
        } else {
          this.focusInput();
        }
      },
      error: (err: HttpErrorResponse) => {
        this.isSending.set(false);
        this.error.set(this.errorMessage(err));
        this.scrollToBottom();
        this.focusInput();
      },
    });
  }

  // Asks the server for a Stripe Checkout page for the new appointment and leaves the app for it.
  // If it fails, the unpaid appointment can still be paid from "Programarile mele" until it expires.
  private startPayment(appointmentId: number): void {
    this.isRedirecting.set(true);
    this.scrollToBottom();

    this.payments.createCheckout(appointmentId).subscribe({
      next: (url) => setTimeout(() => this.payments.redirectTo(url), REDIRECT_DELAY_MS),
      error: (err: HttpErrorResponse) => {
        this.isRedirecting.set(false);
        const reason = err.error?.message ?? 'Nu am putut porni plata.';
        this.error.set(`${reason} Poti plati programarea din "Programarile mele".`);
        this.scrollToBottom();
      },
    });
  }

  // The model sometimes answers with **bold** markdown. We escape the HTML first (so nothing from
  // the model can inject markup), then turn **text** into <strong>. New lines are kept by CSS.
  format(text: string): string {
    const escaped = text
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
    return escaped.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
  }

  private errorMessage(err: HttpErrorResponse): string {
    switch (err.status) {
      case 0:
        return 'Serverul nu raspunde. Verifica conexiunea.';
      case 401:
        return 'Sesiunea a expirat. Autentifica-te din nou.';
      case 503:
        return 'Asistentul nu este disponibil acum. Incearca mai tarziu.';
      case 400:
        return err.error?.message ?? 'Mesajul nu este valid.';
      default:
        return 'A aparut o eroare. Incearca din nou.';
    }
  }

  // Runs after Angular has rendered the new message
  private scrollToBottom(): void {
    setTimeout(() => {
      const element = this.scroller()?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    });
  }

  private focusInput(): void {
    setTimeout(() => this.inputBox()?.nativeElement.focus());
  }
}
