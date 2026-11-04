import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { NgModule, provideBrowserGlobalErrorListeners } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { BrowserModule } from '@angular/platform-browser';
import { App } from './app';
import { AppRoutingModule } from './app-routing-module';
import { ChatWidget } from './components/chat-widget/chat-widget';
import { authInterceptor } from './core/auth.interceptor';

@NgModule({
  declarations: [App],
  imports: [BrowserModule, AppRoutingModule, MatToolbarModule, MatButtonModule, MatMenuModule, ChatWidget],
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withInterceptors([authInterceptor])),
  ],
  bootstrap: [App],
})
export class AppModule {}
