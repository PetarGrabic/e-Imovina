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

Baza (SQLite, `eImovina.Api/App_Data/eimovina.db`) se **automatski migrira i puni seed podacima
pri svakom pokretanju `Api`-ja u razvojnom okruženju** (`DatabaseInitializer`, pozvan iz
`Program.cs`) — nije potrebno ručno pokretati `dotnet ef database update`.

Postojeće migracije (`eImovina.Api/Data/Migrations/`):

1. `InitialCreate` — početna shema: 7 lookup tablica + `AppRoles`, `Locations`, `Employees`,
   `Equipment`, `EquipmentAssignments`, `Inventories`, `InventoryItems`, `EquipmentRequests`,
   `WriteOffRequests`, `EquipmentFiles`, `AppUsers`, `AppUserRoles` (vidi `docs/database.dbml`).
2. `RemoveEmployeeEmailAndJobTitle` — uklonjena nepotrebna polja `Email`/`JobTitle` sa
   `Employees` (nisu tražena specifikacijom, a `Email` je bio čisti duplikat vezanog
   `AppUsers.Email`).

**Pokretanje iz prazne baze:** obrišite `eImovina.Api/App_Data/eimovina.db*` (i po želji
`eImovina.Api/App_Data/uploads/`) te ponovno pokrenite `Api` — migracije i seed se primjenjuju
automatski, bez dupliciranja pri sljedećim pokretanjima (seed je idempotentan).

## Demo računi

Lozinka je ista za sve demo račune: **`Demo123!`**

| Korisničko ime     | Lozinka    | Uloge                         | Zaposlenik / lokacija                   |
| ------------------ | ---------- | ----------------------------- | --------------------------------------- |
| `admin`            | `Demo123!` | Admin                         | —                                       |
| `inventar.manager` | `Demo123!` | InventoryManager              | —                                       |
| `marko.novak`      | `Demo123!` | LocationResponsible, Employee | Marko Novak — Centralno skladište       |
| `ana.kovacic`      | `Demo123!` | LocationResponsible, Employee | Ana Kovačić — OŠ Manuš                  |
| `petra.babic`      | `Demo123!` | LocationResponsible, Employee | Petra Babić — Ispostava Sinj            |
| `ivan.horvat`      | `Demo123!` | Employee                      | Ivan Horvat — Županijska zgrada - Split |
| `luka.peric`       | `Demo123!` | Employee                      | Luka Perić — Županijska zgrada - Split  |

Računi bez elevirane uloge (`Employee`) služe kao primjer odbijenog ownership scenarija — takav
korisnik nema pristup upravljačkim ekranima niti tuđim zaduženjima/zahtjevima. `LocationResponsible`
računi su ograničeni na inventure vlastite lokacije (određene preko gornje veze na zaposlenika).

## Uloge

- **Admin** — puni pristup sustavu: upravljanje korisničkim računima, ulogama i vezama sa
  zaposlenicima (`UsersController`, `EmployeesController`, politika `AdminOnly`), te sve što smiju
  i `InventoryManager` i `LocationResponsible`.
- **InventoryManager** — svakodnevni rad s imovinom: unos/uređivanje opreme, lokacije, zaduženja
  (zaduživanje, povrat, prijenos, promjena lokacije), obrada zahtjeva zaposlenika za opremu i
  otpis, pregled operativnog dashboarda (politika `InventoryManagement`, dijeli je s `Admin`-om).
- **LocationResponsible** — otvara i provodi inventure, ali isključivo za lokaciju na kojoj je
  vezani zaposlenik odgovorna osoba (politika `LocationWork`; API sam ograničava rezultate i akcije
  na tu lokaciju, neovisno o tome što korisničko sučelje prikazuje).
- **Employee** — osobni prikaz vlastite opreme i zahtjeva: `Moja oprema` i `Moji zahtjevi` čitaju
  identitet isključivo iz JWT-a (`/mine` endpointi), bez elevirane politike — nema pristup tuđim
  podacima niti upravljačkim ekranima.

Jedan korisnički račun može imati više uloga istovremeno (npr. `LocationResponsible` + `Employee`
kod svih vezanih zaposlenika iznad).

## Moduli

- **Prijava** — `Login` (javna stranica), `AccessDenied` (403 unutar aplikacije).
- **Početna** — `Home`: operativni dashboard (agregati, vrijednost opreme po lokaciji, zadnje
  promjene) za `Admin`/`InventoryManager`, osobni brojači za ostale uloge.
- **Lokacije** — `Locations`: CRUD nad lokacijama županije (`InventoryManagement`).
- **Oprema** — `Equipment` (popis s pretragom/filterima/sortiranjem), `EquipmentCreate`,
  `EquipmentEdit`, `EquipmentProfile` (detalji, povijest zaduženja, povijest lokacije/statusa,
  vremenska crta, QR kod, datoteke, zahtjevi za otpis), `ScanEquipment` (pronalazak/unos opreme
  skeniranjem QR koda ili ručnim unosom inventurnog broja) — sve `InventoryManagement`.
- **Zaduženja** — `Assignments`: zaduživanje, povrat, prijenos drugom zaposleniku, promjena
  lokacije (`InventoryManagement`); `MyEquipment`: osobni prikaz trenutačno zadužene opreme (svaka
  uloga, `/mine`).
- **Inventure** — `Inventories` (popis, otvaranje/zaključavanje), `InventoryDetails` (obrada
  stavki, odstupanja) — `LocationWork`, ograničeno na vlastitu lokaciju za `LocationResponsible`.
- **Zahtjevi za opremu** — `EquipmentRequests`: pregled i odlučivanje o zahtjevima
  (`InventoryManagement`); `MyRequests`: slanje i pregled vlastitih zahtjeva (svaka uloga, `/mine`).
- **Zahtjevi za otpis** — `WriteOffRequests`: pokretanje, odlučivanje i provedba otpisa opreme
  (`InventoryManagement`).
- **Korisnici** — `Users`: upravljanje korisničkim računima, ulogama i vezom sa zaposlenikom
  (`AdminOnly`).
- **Analitika** — `Analytics`: inventurna odstupanja po lokaciji (preko svih zaključanih
  inventura) i vrijednost opreme po kategoriji/lokaciji (`InventoryManagement`).

### Bonus dio

Uz obavezni dio implementirana su i sva 4 bonus proširenja: detaljna povijest lokacije i statusa
svakog komada opreme, analiza inventurnih odstupanja i vrijednosti opreme po lokacijama (stranica
`Analytics`), vremenska crta opreme s dokumentima i promjenama (na `EquipmentProfile`), te
unos/pronalazak opreme skeniranjem QR koda (`ScanEquipment`, s ručnim unosom kao alternativom
kameri). Detalji u `docs/verification.md`.
