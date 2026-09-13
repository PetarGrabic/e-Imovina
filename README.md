# e-Imovina Županije

Sustav za evidenciju i upravljanje županijskom imovinom — oprema, lokacije, zaduženja,
inventure, zahtjevi zaposlenika i otpisi.

Solution ima tri projekta:

- **`eImovina.App`** — Blazor Web App (`Microsoft.NET.Sdk.Web`, global InteractiveServer) + MudBlazor sučelje.
- **`eImovina.Api`** — ASP.NET Core Web API, EF Core + SQLite, autentikacija, autorizacija, poslovna logika, rad s datotekama.
- **`eImovina.Shared`** — DTO modeli i zajednički tipovi.

## Preduvjeti

- .NET SDK 10 (`net10.0`).
- HTTPS dev certifikat: `dotnet dev-certs https --trust`.

## Pokretanje

1. Postaviti JWT tajne (jednom, iz `eImovina.Api/` direktorija):
   ```
   dotnet user-secrets init
   dotnet user-secrets set "Jwt:Issuer" "eImovina.Api"
   dotnet user-secrets set "Jwt:Audience" "eImovina.App"
   dotnet user-secrets set "Jwt:Key" "<nasumičan string, minimalno 32 znaka>"
   ```
2. API: `dotnet run --project eImovina.Api` → `https://localhost:7150` (Swagger na `/swagger`).
   Baza se pritom automatski kreira, migrira i puni demo podacima.
3. App: `dotnet run --project eImovina.App` → `https://localhost:7100`.

## Migracije baze

_Dopunit će se._

## Demo računi

Lozinka je ista za sve demo račune: **`Demo123!`**

| Korisničko ime     | Lozinka    | Uloge                         |
| ------------------ | ---------- | ----------------------------- |
| `admin`            | `Demo123!` | Admin                         |
| `inventar.manager` | `Demo123!` | InventoryManager              |
| `marko.novak`      | `Demo123!` | LocationResponsible, Employee |
| `ana.kovacic`      | `Demo123!` | LocationResponsible, Employee |
| `ivan.horvat`      | `Demo123!` | Employee                      |
| `petra.babic`      | `Demo123!` | LocationResponsible, Employee |
| `luka.peric`       | `Demo123!` | Employee                      |

## Uloge

_Dopunit će se._

## Moduli

_Dopunit će se._
