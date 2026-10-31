# PCO Barbershop

Platforma web de rezervari si management pentru frizerie.
Angular (SPA) + ASP.NET Core Web API (.NET 10) + PostgreSQL.

## Cerinte de instalat (pe un calculator nou)

- Visual Studio Community 2026 (workload: ASP.NET and web development)
- .NET 10 SDK
- Node.js LTS (include npm)
- Git
- PostgreSQL (Server, pgAdmin 4, Command Line Tools), port 5432

Unelte globale (se instaleaza o singura data):

```
npm install -g @angular/cli
dotnet tool install --global dotnet-ef
```

## Pachete backend (NuGet)

Se instaleaza automat la build (`dotnet restore`), deoarece sunt declarate in `.csproj`.
Comenzile de mai jos sunt necesare doar daca vrei sa le adaugi manual din terminal,
rulate in folderul `PCOBarbershop.Server`.

**Baza de date (PostgreSQL + EF Core)**

- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.EntityFrameworkCore.Design

```
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design
```

**Autentificare si autorizare**

- Microsoft.AspNetCore.Identity.EntityFrameworkCore
- Microsoft.AspNetCore.Authentication.JwtBearer
- Microsoft.AspNetCore.Authentication.Google

```
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.AspNetCore.Authentication.Google
```

**2FA (QR pentru Google Authenticator)**

- QRCoder

```
dotnet add package QRCoder
```

**Notificari e-mail**

- MailKit

```
dotnet add package MailKit
```

**Plati**

- Stripe.net

```
dotnet add package Stripe.net
```

**Documentare API (Swagger UI)**

- Swashbuckle.AspNetCore.SwaggerUI

```
dotnet add package Swashbuckle.AspNetCore.SwaggerUI
```

## Pachete frontend (npm)

Se instaleaza cu `npm install` in folderul `pcobarbershop.client`.
Comenzile de mai jos sunt necesare doar la adaugarea manuala.

- @angular/material, @angular/cdk
- @stripe/stripe-js

```
ng add @angular/material
npm install @stripe/stripe-js
```

## Servicii externe

- Google Cloud Console: OAuth Client ID (Google Sign-In)
- Stripe: cont in test mode (+ Stripe CLI pentru webhook-uri)
- SMTP pentru dezvoltare (Mailtrap / Papercut)
- Google Authenticator (pe telefon, pentru testat 2FA)

## Rulare

1. Cloneaza repository-ul
2. In `pcobarbershop.client`: `npm install`
3. Aproba scripturile de build ale npm daca apare avertismentul `allow-scripts`
   (esbuild, lmdb, @parcel/watcher, msgpackr-extract): `npm approve-scripts <pachet>`
4. Creeaza baza de date in PostgreSQL si seteaza secretele (vezi mai jos)
5. Aplica migrarile, din folderul Server: `dotnet ef database update`
6. Deschide solutia in Visual Studio si apasa F5
   (accepta certificatul HTTPS de dezvoltare daca apare)

Swagger UI (documentatia si testarea API-ului) este la `https://localhost:7077/swagger`.
Pentru rutele protejate: login din `/api/auth/login`, apoi butonul **Authorize** cu tokenul primit.

## Functionalitati

**Clienti**
- cont cu e-mail + parola, confirmat cu un cod OTP de 6 cifre trimis pe e-mail
- autentificare cu contul Google (Google Sign-In)
- autentificare in doi pasi (2FA / TOTP) cu Google Authenticator, cu coduri de recuperare (pagina Securitate)
- blocare temporara a contului dupa 5 incercari gresite
- alegerea serviciului, a frizerului si a unei ore libere, plata cu cardul prin Stripe Checkout
- pagina "Programarile mele"; ora ramane rezervata 30 de minute, iar daca nu se plateste se elibereaza automat

**Frizeri**
- calendarul saptamanal al rezervarilor
- editarea programului de lucru pe zile
- anularea unei programari: clientul primeste e-mail, iar daca programarea era platita banii se returneaza automat

## Secrete

Nu se pun in repository. Se salveaza local cu User Secrets
(click dreapta pe proiectul Server > Manage User Secrets).
Setarile fara secrete (port SMTP, expeditor, moneda etc.) sunt in `appsettings.json`.

```json
{
  "ConnectionStrings": { "DefaultConnection": "Host=localhost;Port=5432;Database=pcobarbershop;Username=...;Password=..." },
  "Jwt": { "Key": "...minim 32 de caractere...", "Issuer": "PCOBarbershop", "Audience": "PCOBarbershopClient", "ExpiresInMinutes": 60 },
  "Smtp": { "Host": "sandbox.smtp.mailtrap.io", "Port": 587, "Username": "...", "Password": "..." },
  "Stripe": { "SecretKey": "sk_test_...", "WebhookSecret": "whsec_..." },
  "Google": { "ClientId": "....apps.googleusercontent.com" }
}
```

Fara `Smtp:Host`, e-mailurile (inclusiv codurile de confirmare) nu se trimit, ci apar in consola serverului.
Fara `Stripe:SecretKey` plata nu porneste, iar fara `Google:ClientId` butonul Google nu apare.

## Configurarea serviciilor externe

**Stripe (test mode)**
1. Cont pe https://dashboard.stripe.com, cu **Test mode** activ
2. Developers > API keys: copiaza **Secret key** (`sk_test_...`) in `Stripe:SecretKey`
3. Webhook local cu Stripe CLI (optional; fara el plata se confirma oricum la intoarcerea pe site):
   ```
   stripe login
   stripe listen --forward-to https://localhost:7077/api/payments/webhook --skip-verify
   ```
   Copiaza `whsec_...` afisat in `Stripe:WebhookSecret`
4. Card de test: `4242 4242 4242 4242`, orice data viitoare, orice CVC

**Google Sign-In**
1. https://console.cloud.google.com > APIs & Services > OAuth consent screen (External, adauga-te la Test users)
2. Credentials > Create credentials > OAuth client ID > **Web application**
3. Authorized JavaScript origins: `https://localhost:49297` si `http://localhost:49297`
4. Copiaza Client ID in `Google:ClientId`

**SMTP (Mailtrap)**
1. Cont pe https://mailtrap.io > Email Testing > Inbox > SMTP Settings
2. Copiaza Host, Port, Username, Password in sectiunea `Smtp`
