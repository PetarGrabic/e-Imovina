# Verifikacija — e-Imovina Županije

Ovaj dokument prolazi kroz popis obaveznih provjera prije predaje projekta i za svaku stavku
navodi konkretan dokaz da rješenje radi kako treba — uz status kod, poruku ili opažanje sa
stvarnog pokretanja aplikacije. Sve je provjereno na aplikaciji koja se pokreće isključivo iz
`dotnet run` — nema ovisnosti o razvojnim test-skriptama koje ne ulaze u predaju.

## Predaja projekta

- **Izvorni kod `App`, `Api`, `Shared` + solution datoteka** — `eImovina.slnx` na korijenu
  repozitorija, tri projekta uz njega.
- **Sve EF Core migracije i seed podatke** — `eImovina.Api/Data/Migrations/` (`InitialCreate`,
  `RemoveEmployeeEmailAndJobTitle`); `eImovina.Api/Data/DemoDataSeeder.cs` (idempotentan seed
  lookup i demo podataka, pokreće se automatski pri startu `Api`-ja).
- **`README.md` s uputama** — pokretanje, migracije, demo računi, uloge, moduli (vidi README.md).
- **Demo korisnici za sve uloge** — 7 računa u `README.md`/`DemoDataSeeder.cs`, pokrivaju sve 4
  uloge (uklj. dva multi-role primjera: `LocationResponsible` + `Employee`).
- **Konačni DBML baze** — `docs/database.dbml`, provjeren red po red protiv stvarnog EF Core
  modela (21 entitet, sve migracije, uklj. bonus tablice `EquipmentLocationHistories`/
  `EquipmentStatusHistories`) i usklađen s njim u potpunosti.
- **Popis provjerenih workflowa i autorizacijskih scenarija** — ovaj dokument.

## Prije predaje obavezno provjerite

### `dotnet build` / build solutiona bez grešaka

✅ `dotnet build eImovina.slnx` → **0 Warning(s), 0 Error(s)** (provjereno više puta tijekom
razvoja, uklj. nakon svake izmjene koda).

### Zajedničko pokretanje App i API projekta

✅ `dotnet run --project eImovina.Api` (`https://localhost:7150`, Swagger na `/swagger`) i
`dotnet run --project eImovina.App` (`https://localhost:7100`) pokrenuti istovremeno; `App` sve
podatke dohvaća preko `HttpClient`-a prema `Api`-ju.

### Kreiranje prazne baze iz svih migracija

✅ Obrisan `eImovina.Api/App_Data/eimovina.db*` + `uploads/`; pri sljedećem pokretanju `Api`-ja
baza je automatski migrirana i napunjena (potvrđeno preko `/api/equipment`, `/api/locations`,
`/api/users` odmah nakon starta: **12 opreme, 4 lokacije, 7 korisnika** — točni seed brojevi).

### Seed lookup i demo podataka bez dupliciranja

✅ `Api` ponovno pokrenut **bez** brisanja baze — identični brojevi (12/4/7) nakon restarta,
potvrđuje da `DemoDataSeeder`-ov `AnyAsync`-guard sprječava dupliciranje.

### Login i logout

✅ Prijava kroz `/login` provjerena za `admin`, `ivan.horvat` (Employee) i `marko.novak`
(LocationResponsible + Employee) — svaki uspješno preusmjeren na `/`. Odjava (izbornik računa →
"Odjava") provjerena nakon prijave kao `admin` — preusmjerenje na `/login` potvrđeno.

### Navigacija prema ulogama

✅ Bočni izbornik provjeren uživo za tri uloge:

- **Admin**: svih 10 stavki (Početna, Inventure, Lokacije, Oprema, Zaduženja, Zahtjevi za opremu,
  Zahtjevi za otpis, Moja oprema, Moji zahtjevi, Korisnici).
- **Employee** (`ivan.horvat`): samo Početna, Moja oprema, Moji zahtjevi — nema pristupa
  upravljačkim modulima ni "Korisnici".
- **LocationResponsible** (`marko.novak`): Početna, Inventure, Moja oprema, Moji zahtjevi — vidi
  Inventure (svoju lokaciju), ali ne i Lokacije/Oprema/Zaduženja/Zahtjevi/Korisnici.

Skrivanje stavke izbornika nije jedina zaštita — svaki od tih endpointa dodatno provjerava
autorizaciju na API strani (vidi dolje).

### Oba glavna poslovna workflowa

✅ Oba provjerena end-to-end kroz sučelje kao `inventar.manager`:

1. **Životni ciklus opreme**: kreirana nova stavka (`INV-S17-WF`, validacija obaveznih polja
   provjerena praznim submitom prije popunjavanja) → zadužena zaposleniku (Ivan Horvat) → prenesena
   drugom zaposleniku (Luka Perić, prethodno zaduženje ostaje u povijesti kao "Premješteno") →
   promijenjena lokacija (Centralno skladište → Ispostava Sinj, aktivno zaduženje ostaje netaknuto,
   točno prema pravilu da sama promjena lokacije ne mora završiti zaduženje) → vraćena (potvrda
   prije akcije, povijest zaduženja potpuno sačuvana: Luka Perić "Vraćeno", Ivan Horvat
   "Premješteno").
2. **Životni ciklus inventure**: postojeća inventura u statusu "Nacrt" (Centralno skladište,
   Marko Novak) → otvorena (potvrda "ova se radnja ne može poništiti", formirano 8 stavki prema
   trenutačnoj opremi na lokaciji) → obrađeno svih 8 stavki (mix ishoda: 7 pronađeno, 1 nedostaje,
   1 oštećeno, uklj. jedna stavka pronađena na pogrešnoj lokaciji — 3 odstupanja ukupno, sažetak na
   ekranu točno odgovara) → završena → zaključana (potvrda "trajno zaključana, nepovratna radnja").

### Barem jedan uspješan i jedan odbijeni ownership scenarij

✅ (curl s pravim JWT-om) `marko.novak` (LocationResponsible, vlastita lokacija =
Centralno skladište, id 3): `POST /api/inventories {"locationId":3,...}` → **201 Created**;
identičan poziv s `{"locationId":1,...}` (tuđa lokacija) → **403 Forbidden**,
`"Možete kreirati inventuru samo za vlastitu lokaciju."` — `InventoriesController.
CallerCanAccessLocationAsync` provodi granicu na API-ju, ne samo u sučelju.

### Razlika između 401 Unauthorized i 403 Forbidden

✅ `GET /api/equipment` bez `Authorization` zaglavlja → **401**; isti poziv s
valjanim JWT-om uloge `Employee` (nema `InventoryManagement` politiku) → **403**. Jasna razlika
"nisi prijavljen" vs. "prijavljen si, ali nemaš ovlast" na istom endpointu.

### `/mine` stranice i pripadajući API endpointi

✅ Za `GET /api/assignments/mine`, `GET /api/equipmentrequests/mine`,
`GET /api/dashboard/mine`: identičan poziv s i bez pokušaja nadjačavanja identiteta preko query
parametra (`?employeeId=3`, tuđi id, dok je prijavljen `ivan.horvat` s `EmployeeId=1`) vratio je
**bit-identičan odgovor** oba puta — identitet dolazi isključivo iz JWT `EmployeeId` claima,
klijent ga ne može zaobići. Stranice `MyEquipment`/`MyRequests`/osobni dio `Home` vizualno
potvrđene da prikazuju točno te podatke za `ivan.horvat` i `marko.novak`.

### Pretragu, kombinirane filtere, sortiranje i reset filtera

✅ Provjereno uživo na `Equipment` listi (jedan od najmanje dva glavna tablična prikaza s ovom
funkcionalnošću — drugi je `InventoryDetails`' stavke inventure):

- Tekstualna pretraga ("S17") → suzila popis na točno 1 red.
- Kombinacija pretrage + kategorije ("Alat") → 0 rezultata (ispravna AND logika, budući da
  testna stavka nije kategorije Alat) → prikazano prazno stanje "Nema opreme za prikaz."
- Sortiranje po stupcu "Naziv" → strelica smjera sortiranja se pojavljuje, poredak retka
  potvrđeno abecedni.
- "Poništi filtere" → svi filteri vraćeni na početne vrijednosti, puni popis opreme ponovno
  prikazan.
- Filtriranje/sortiranje ide preko API query parametara (`GET /api/equipment?...`), ne lokalno u
  Blazor komponenti — dio je izvorne implementacije servera (`EquipmentController`/`EquipmentQuery`).

### Loading, empty, error i validation stanja

✅ Provedena je cjelovita revizija svih stranica i dijaloga te popravljene preostale praznine
(loading indikator dok se učitavaju padajući izbornici u 9 dijaloga, pravi `MudForm`/
DataAnnotations umjesto ručnih provjera u 2 dijaloga, `OnRetry` na jednoj `ErrorAlert` komponenti).
Dodatno je uživo potvrđeno:

- **Validation**: prazan submit forme za novu opremu → 3 crvene poruke po polju
  ("Inventurni broj je obavezan.", "Naziv je obavezan.", "Potrebno je odabrati kategoriju/
  status/lokaciju."); `InventoryItemEditDialog`'s "pronađeno ⇒ lokacija obavezna" pravilo
  potvrđeno (crvena poruka, submit blokiran dok se lokacija ne odabere).
- **Empty**: prazan popis datoteka/povijesti zaduženja/zahtjeva za otpis na profilu tek
  kreirane opreme ("Još nema datoteka.", "Još nema zaduženja.", "Još nema zahtjeva za otpis.");
  prazan popis opreme nakon filtriranja na nepostojeću kombinaciju ("Nema opreme za prikaz.").
- **Loading/Error**: dijaloške padajuće liste (npr. "Zaduži opremu", "Promijeni lokaciju")
  učitavaju se bez praznog treptaja zahvaljujući `LoadingPanel` komponenti koja se prikazuje dok
  se podaci dohvaćaju.

### Upload dopuštene datoteke i odbijanje nedopuštene datoteke

✅ Provjereno uživo na profilu opreme, i klijentski i na API-ju izravno:

- Dopuštena datoteka (mala validna PNG slika) → uspješno prihvaćena, thumbnail prikazan.
- Nedopušteno proširenje (`.exe`) → sučelje: crvena poruka "Za sliku su dopušteni formati: PNG,
  JPG, WEBP."; API izravno (`POST /api/equipment/{id}/files`, zaobilazeći sučelje) → **400**,
  `"Za sliku su dopušteni formati: PNG, JPG, WEBP."` — provjera postoji i na API strani, ne samo
  u Blazoru.
- Prevelika datoteka (11 MB, limit je 10 MB) → sučelje: crvena poruka "Datoteka je prevelika
  (najviše 10 MB)."; API izravno → **400** (Kestrel/form-size limit odbija zahtjev prije nego što
  i stigne do poslovne logike, still 400, ne 200).
- Brisanje datoteke uklanja i fizički zapis i DB metapodatke (`FilesController.DeleteFile` briše
  oboje u istoj operaciji).

### Dashboard agregate

✅ Manager dashboard (`admin` prijava): kartice "Ukupno opreme" (12), "Zadužena
oprema" (4), "Na servisu" (1), "Nedostaje" (1), "Otvoreni zahtjevi" (3), "Otvorene inventure" (1),
"Ukupna evidentirana vrijednost" (21040.00), graf "Vrijednost opreme po lokaciji" — svi brojevi
odgovaraju stvarnom stanju baze u tom trenutku, agregate računa API (`DashboardController`), ne
frontend. Osobni dashboard za `ivan.horvat` (Employee) i `marko.novak` (LocationResponsible)
potvrđen s točnim, ulogom-specifičnim brojačima (uklj. `marko.novak`-ov "Otvorene inventure na
mojoj lokaciji" — tile koji plain `Employee` uopće ne vidi).

### Provjera da lozinke i JWT tajne nisu zapisane kao čisti tekst niti predane u repozitorij

✅ `eImovina.Api/appsettings.json`'s `Jwt:Issuer`/`Audience`/`Key`
su prazni placeholderi u repozitoriju; `eImovina.Api.csproj` ima pravi `UserSecretsId`, što znači
da stvarni ključ živi isključivo u `dotnet user-secrets` na razvojnom stroju. Lozinke se hashiraju
kroz `PasswordHasher<AppUser>` (ASP.NET Core), nikad ne spremaju kao čisti tekst — potvrđeno u
`AppUser.PasswordHash` shemi i `UsersController`/`AuthController` kodu. JWT token prema
pregledniku: nakon prijave kao `admin`, `document.documentElement.outerHTML` ne sadrži nikakav
JWT-oblik niz, `localStorage`/`sessionStorage` prazni, `document.cookie` vraća prazan string
(autentikacijski kolačić je `HttpOnly`, nevidljiv JavaScriptu) — token ostaje isključivo na
poslužiteljskoj strani `App`-a, nikad ne stiže do preglednika.

## Poslovna pravila ("Pravila koja moraju raditi")

Svako od 10 obaveznih poslovnih pravila provjereno je uživo (curl s pravim JWT-om, ne samo
čitanjem koda), s konkretnim odgovorom API-ja:

| #   | Pravilo                                                      | Uživo test                                                                                            | Rezultat                                                                                                                                                                          |
| --- | ------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Inventurni broj jedinstven                                   | `POST /api/equipment` s postojećim `inventoryNumber` (`INV-0005`)                                     | **PROLAZI** — `409`, `"Oprema s inventurnim brojem 'INV-0005' već postoji."`                                                                                                      |
| 2   | Serijski broj jedinstven ako je unesen                       | Kreirana oprema sa `serialNumber:"SN-VERIFY-001"`, zatim druga s istim serijskim brojem               | **PROLAZI** — `409`, `"Oprema sa serijskim brojem 'SN-VERIFY-001' već postoji."`                                                                                                  |
| 3   | Otpisana oprema ne može se zadužiti                          | `POST /api/assignments` na seed-anu `Otpisano` stavku                                                 | **PROLAZI** — `409`, `"Oprema nije na skladištu pa se ne može zadužiti."`                                                                                                         |
| 4   | Oprema ne može imati dva aktivna zaduženja                   | `POST /api/assignments` na opremu koja je već aktivno zadužena                                        | **PROLAZI** — `409` (isti provjera statusa; dodatni DB filtrirani unique indeks kao zaštita od utrke zahtjeva)                                                                    |
| 5   | Povrat ne može biti prije zaduženja                          | `POST /api/assignments/{id}/return` s pokušajem slanja `returnedAtUtc` prije stvarnog `assignedAtUtc` | **PROLAZI** — `204`; API u potpunosti ignorira klijentski poslani datum povrata i uvijek bilježi trenutačno vrijeme poslužitelja, koje po definiciji ne može prethoditi zaduženju |
| 6   | Odgovorna osoba mijenja samo inventuru svoje lokacije        | `POST /api/inventories` na vlastitoj lokaciji → `201`; identičan poziv na tuđoj lokaciji → `403`      | **PROLAZI** (vidi "Barem jedan uspješan i jedan odbijeni ownership scenarij" iznad)                                                                                               |
| 7   | Zaposlenik vidi samo vlastita zaduženja i zahtjeve           | `/mine` endpointi identitet čitaju isključivo iz JWT-a, klijentski parametar nema efekta              | **PROLAZI** (vidi "`/mine` stranice..." iznad)                                                                                                                                    |
| 8   | Zaključana inventura više se ne može mijenjati               | `PUT /api/inventories/{id}/items/{itemId}` na zaključanoj inventuri                                   | **PROLAZI** — `409`, `"Inventura je zaključana pa se stavke više ne mogu mijenjati."`                                                                                             |
| 9   | Provedeni otpis postavlja `Otpisano` i onemogućuje zaduženje | Zahtjev za otpis → odobren → proveden → status opreme `Otpisano` → pokušaj zaduženja                  | **PROLAZI** — provedba vraća `200`/`"Provedeno"`, status potvrđen `Otpisano`, naknadni pokušaj zaduženja `409`                                                                    |
| 10  | Drugi otvoreni zahtjev za otpis iste opreme blokiran         | Drugi `POST /api/writeoffrequests` za istu opremu dok je prvi još otvoren                             | **PROLAZI** — `409`, `"Oprema već ima otvoren zahtjev za otpis."` (DB filtrirani unique indeks)                                                                                   |

Uz to, anonimni i neovlašteni pozivi dosljedno vraćaju `401` ili `403` (vidi "Razlika između 401
Unauthorized i 403 Forbidden" iznad), a skrivanje akcije u Blazoru nigdje nije jedina zaštita —
svaki zaštićeni endpoint ponavlja istu provjeru autorizacije na API strani, neovisno o tome što
sučelje prikazuje.

## Bonus dio

Sva 4 bonus proširenja implementirana su i provjerena u pregledniku (Playwright CLI) nakon punog
pokretanja iz prazne baze:

- **Detaljna povijest lokacije i statusa opreme** — nove tablice `EquipmentLocationHistories`/
  `EquipmentStatusHistories`, popunjavaju se pri svakoj promjeni (unos opreme, ručno uređivanje,
  promjena lokacije, zaduženje/povrat, provedba otpisa). Provjereno: promjena lokacije i ciklus
  zaduži/vrati na stvarnoj opremi ispravno stvaraju retke u oba panela na `EquipmentProfile`
  stranici (`Povijest lokacije`, `Povijest statusa`), ispravnim redoslijedom i podacima
  (prije → poslije, tko, kada).
- **Analiza inventurnih odstupanja i vrijednosti opreme po lokacijama** — nova `/analytics`
  stranica; `GET /api/analytics/discrepancies-by-location` agregira odstupanja preko svih
  zaključanih inventura, `GET /api/analytics/value-by-category` po kategoriji. Provjereno: stranica
  ispravno prikazuje prazno stanje (u demo bazi nema zaključane inventure) i ispravno prikazuje
  graf vrijednosti po kategoriji te tablicu vrijednosti po lokaciji, bez grešaka u konzoli.
- **Vremenska crta opreme** — `GET /api/equipment/{id}/timeline` spaja zaduženja, povijest
  lokacije/statusa, zahtjeve za otpis i uploadane datoteke u jedan kronološki prikaz
  (`MudTimeline` na `EquipmentProfile`). Provjereno: nakon promjene lokacije i zaduženja, oba
  događaja odmah se pojavljuju na vremenskoj crti ispravnim redoslijedom.
- **QR kod — unos i pronalazak opreme** — `GET /api/equipment/{id}/qrcode` (QRCoder, kodira samo
  inventurni broj) prikazan na `EquipmentProfile`; nova `/equipment/scan` stranica nudi kameru
  (vendored `jsQR`, prvi JS interop u aplikaciji) i ručni unos koda kao alternativu. Provjereno
  (ručni unos, kamera provjerena vizualno jer headless preglednik nema pravu kameru): postojeći
  inventurni broj vodi izravno na profil opreme; nepostojeći broj nudi "Kreiraj novu opremu s ovim
  brojem", koji ispravno predispunjava formu za novu opremu.

## Sažetak

Svih 15 stavki "Prije predaje obavezno provjerite" i svih 6 stavki "Predaja projekta" provjereno
i potvrđeno funkcionalnim, s konkretnim dokazima navedenim iznad. Puna autorizacijska i
poslovno-pravna matrica (10/10 pravila iz "Pravila koja moraju raditi" gore, po-ulozi dopušteno/
odbijeno, razlika 401 vs 403) provjerena je uživo i dokumentirana u cijelosti u ovom dokumentu.
