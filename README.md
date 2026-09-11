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

_Dopunit će se — App i API se pokreću zajedno (npr. `dotnet run` u dva terminala)._

- API: `dotnet run --project eImovina.Api` → `https://localhost:7150` (Swagger na `/swagger`).
- App: `dotnet run --project eImovina.App` → `https://localhost:7100`.

## Migracije baze

_Dopunit će se._

## Demo računi

_Dopunit će se._

## Uloge

_Dopunit će se._

## Moduli

_Dopunit će se._

## Testovi

_Dopunit će se — Playwright E2E._
