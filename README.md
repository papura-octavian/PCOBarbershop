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

## Secrete

Nu se pun in repository. Se salveaza local cu User Secrets
(click dreapta pe proiectul Server > Manage User Secrets):

- ConnectionStrings:DefaultConnection
- Jwt:Key
- Google:ClientId / Google:ClientSecret
- Stripe:SecretKey
- Smtp:Password
