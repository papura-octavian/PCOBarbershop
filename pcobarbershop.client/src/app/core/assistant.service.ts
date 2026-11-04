import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AssistantChatResponse, AssistantMessage } from './assistant.models';

@Injectable({ providedIn: 'root' })
export class AssistantService {
  private readonly http = inject(HttpClient);

  // The model keeps no memory between requests, so we send the whole conversation every time.
  // The token is attached by the auth interceptor.
  chat(messages: AssistantMessage[]): Observable<AssistantChatResponse> {
    return this.http.post<AssistantChatResponse>('/api/assistant/chat', { messages });
  }
}
