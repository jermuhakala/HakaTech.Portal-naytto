# HakaTech.Portal — Käyttöliittymädokumentaatio

> Päivitetty: 2026-04-16  
> Framework: ASP.NET Core 9 MVC · Razor Views · Bootstrap 5

---

## Sisällysluettelo

1. [Arkkitehtuuri ja teknologia](#arkkitehtuuri-ja-teknologia)
2. [Layout ja navigaatio](#layout-ja-navigaatio)
3. [Roolit ja käyttöoikeudet](#roolit-ja-käyttöoikeudet)
4. [Sivut ja toiminnot](#sivut-ja-toiminnot)
   - [Kirjautuminen (Account)](#kirjautuminen-account)
   - [Dashboard (Home)](#dashboard-home)
   - [Tiketit (Ticket)](#tiketit-ticket)
   - [Laskut (Invoice)](#laskut-invoice)
   - [Varaukset (Booking)](#varaukset-booking)
   - [Asiakkaat (Customer)](#asiakkaat-customer)
   - [Yrityksen käyttäjät (CustomerUser)](#yrityksen-käyttäjät-customeruser)
   - [Palvelukatalogi (ServiceCatalog)](#palvelukatalogi-servicecatalog)
   - [Tietopankki (KnowledgeBase)](#tietopankki-knowledgebase)
   - [Etätyöpöytä (RemoteDesktop)](#etätyöpöytä-remotedesktop)
   - [Tiedotteet (Announcement)](#tiedotteet-announcement)
   - [Raportit (Report)](#raportit-report)
   - [Tapahtumaloki (AuditLog)](#tapahtumaloki-auditlog)
5. [Käyttöoikeusmatriisi](#käyttöoikeusmatriisi)
6. [Lomakkeet ja tietovirrat](#lomakkeet-ja-tietovirrat)
7. [Teemat, värit ja responsiivisuus](#teemat-värit-ja-responsiivisuus)
8. [Lokalisaatio](#lokalisaatio)
9. [Näkymätiedostot](#näkymätiedostot)

---

## Arkkitehtuuri ja teknologia

| Ominaisuus | Teknologia |
|-----------|-----------|
| MVC-framework | ASP.NET Core 9 MVC |
| Näkymät | Razor Views (.cshtml) |
| UI-kirjasto | Bootstrap 5 |
| Ikonit | Bootstrap Icons (CDN) |
| Fontit | Inter (Google Fonts, CDN) |
| JavaScript | jQuery + jQuery Validation |
| Reaaliaikaisuus | SignalR (`/hubs/ticket`) — tikettien kommentointi |
| Tyylitiedosto | `wwwroot/css/site.css` (mukautettu, 1400+ riviä) |

---

## Layout ja navigaatio

### Päälayout (`_Layout.cshtml`)

Kaikki kirjautuneiden käyttäjien sivut käyttävät pääasettelua:

```
┌─────────────────────────────────────────────────────────┐
│ Sivupalkki (256 px)      │  Pääsisältöalue              │
│                          │                              │
│  [Logo: HakaTech Portal] │  <h1>Sivun otsikko</h1>      │
│                          │  [Sisältö]                   │
│  Navigaatiolinkit        │                              │
│                          │                              │
│  ─────────────────────   │                              │
│  [Teema-toggle]          │                              │
│  [Vaihda salasana]       │                              │
│  [Kieli: FI / SV / EN]   │                              │
│  [Kirjaudu ulos]         │                              │
└─────────────────────────────────────────────────────────┘
```

**Mobiilissa** sivupalkki piiloutuu ja yläreunaan ilmestyy hampurilaisvalikko.

### Kirjautumislayout (`_AuthLayout.cshtml`)

Kirjautuminen, rekisteröinti ja salasananvaihto käyttävät erillistä asettelua: keskitetty kortti, ei sivupalkkia, liukuväritausta.

### Sivupalkin navigaatio

**Kaikille kirjautuneille:**

| Linkki | URL | Kuvaus |
|--------|-----|--------|
| Yhteenveto | `/` | Dashboard / etusivu |
| Tiketit | `/Ticket` | Tukitiketit |
| Laskut | `/Invoice` | Laskutus |
| Palvelut | `/ServiceCatalog` | Palvelukatalogi |
| Etätyöpöytäyhteys | `/RemoteDesktop` | Etäyhteydet |
| Tietopankki | `/KnowledgeBase` | Ohjeet ja artikkelit |
| Kalenteri | `/Booking` | Varaukset |

**Vain Admin — Hallinta-osio:**

| Linkki | URL |
|--------|-----|
| Asiakkaat | `/Customer` |
| Luo käyttäjä | `/Account/Register` |
| Raportit | `/Report` |
| Tiedotteet | `/Announcement` |
| KB: Hallinta | `/KnowledgeBase/Manage` |
| Tapahtumaloki | `/AuditLog` |

**Vain Customer Admin:**

| Linkki | URL |
|--------|-----|
| Käyttäjät | `/CustomerUser` |

---

## Roolit ja käyttöoikeudet

Portaalissa on kolme tasoa:

| Rooli | Kuvaus |
|-------|--------|
| **Admin** | HakaTech-henkilöstö. Näkee kaiken, hallinnoi kaikkia asiakkaita, luo laskut, raportit, tiedotteet. |
| **Customer** | Asiakasyrityksen tavallinen käyttäjä. Näkee vain oman yrityksensä datan. |
| **CustomerAdmin** | Asiakasyrityksen pääkäyttäjä (`IsCustomerAdmin = true`). Voi hallinnoida oman yrityksensä käyttäjiä. |

> Null `CustomerId` käyttäjässä = Admin. Kaikilla Customer-käyttäjillä on aina CustomerId.

---

## Sivut ja toiminnot

### Kirjautuminen (Account)

#### `GET /Account/Login` — Kirjautuminen
- **Pääsy:** Kaikki (myös kirjautumaton)
- **Sisältö:** Sähköposti, salasana, "Muista minut" -valintaruutu
- **Toiminnot:** Kirjaudu sisään (POST) → ohjataan dashboardille

#### `GET /Account/Register` — Käyttäjän luonti
- **Pääsy:** Admin
- **Sisältö:** Koko nimi, sähköposti, salasana, rooli (Admin/Customer), asiakasyritys (kun rooli = Customer)
- **Toiminnot:** Luo käyttäjä (POST) → ohjataan dashboardille

#### `GET /Account/ChangePassword` — Salasanan vaihto
- **Pääsy:** Kaikki kirjautuneet
- **Sisältö:** Nykyinen salasana, uusi salasana, vahvistus
- **Toiminnot:** Tallenna (POST)

#### `POST /Account/SetLanguage` — Kielivalinta
- **Pääsy:** Kaikki
- **Toiminto:** Asettaa kulttuurievästeen (fi-FI / sv-SE / en-US) ja uudelleenlataa sivun

---

### Dashboard (Home)

#### `GET /` — Etusivu / Yhteenveto
- **Pääsy:** Kaikki kirjautuneet
- **Sisältö (Admineille):**
  - KPI-kortit: Avoimet tiketit, Ratkaisemattomat, Erääntyneet laskut, Aktiiviset asiakkaat
  - Widgetit (järjestys muokattavissa): Viimeisimmät tiketit, Viimeisimmät laskut, Tiedotteet, Sopimustilanne
- **Sisältö (Customerille):**
  - KPI-kortit: Omat avoimet tiketit, Maksamattomat laskut
  - Widgetit: Omat tiketit, Omat laskut, Tiedotteet
- **Toiminnot:** Widgetijärjestyksen tallennus (POST `/Home/SaveLayout`)

> KPI-kortit ovat klikattavia — vievät suodatettuun listaukseen (esim. avoimet tiketit).

---

### Tiketit (Ticket)

#### `GET /Ticket` — Tikettilista
- **Pääsy:** Kaikki kirjautuneet (Customer näkee vain omat)
- **Sisältö:** Taulukko: #, otsikko, status, prioriteetti, asiakas (Admin), luontipäivä, käsittelijä
- **Suodattimet:** Status, prioriteetti, hakusana
- **Toiminnot:** Linkki uuteen tikettiin, riviltä tikettiin

#### `GET /Ticket/Create` — Uusi tiketti
- **Pääsy:** Kaikki kirjautuneet
- **Lomake:**
  - Otsikko (max 300 merkkiä)
  - Kuvaus (tekstialue)
  - Kategoria: Network / Hardware / Software / Email / Access / Other
  - Prioriteetti: Low / Normal / High / Critical
- **Toiminnot:** Lähetä (POST)

#### `GET /Ticket/{id}` — Tiketti
- **Pääsy:** Kaikki kirjautuneet (Customer vain omansa)
- **Sisältö:**
  - Tikettitiedot (status, prioriteetti, kategoria, käsittelijä, luontiaika)
  - Kommenttiketju (sisäiset kommentit piilotettu Customerilta)
  - Liitetiedostot (lataus + poisto Adminille)
  - Palautelomake (1–5 tähteä + kommentti — näytetään kun tiketti on suljettu)
- **Admin-toiminnot:** Status, prioriteetti, käsittelijä (POST `/Ticket/UpdateStatus`)
- **Kaikki:** Lisää kommentti (POST `/Ticket/AddComment`), Lähetä liite, Sulje tiketti

---

### Laskut (Invoice)

#### `GET /Invoice` — Laskujen lista
- **Pääsy:** Kaikki kirjautuneet (Customer vain omansa)
- **Sisältö:** Laskun #, asiakas, päiväys, eräpäivä, loppusumma (alv:lla), status
- **Suodattimet:** Status, asiakas (Admin)

#### `GET /Invoice/{id}` — Lasku
- **Pääsy:** Kaikki kirjautuneet (Customer vain omansa)
- **Sisältö:** Laskurivit (kuvaus, määrä, à-hinta), välisumma, ALV (25,5 %), kokonaissumma, liitteet
- **Toiminnot:** Lataa PDF (GET `/Invoice/DownloadPdf/{id}`)
- **Admin-toiminnot:** Muuta status, lisää liite, poista liite

#### `GET /Invoice/Create` — Uusi lasku
- **Pääsy:** Admin
- **Lomake:** Laskun #, asiakas, päiväys, eräpäivä, ALV-kanta, laskurivit (dynaaminen lisäys/poisto), lisätiedot
- **Toiminnot:** Tallenna (POST) → lähettää sähköposti-ilmoituksen asiakkaalle

---

### Varaukset (Booking)

#### `GET /Booking` — Kalenteri (Customer)
- **Pääsy:** Customer
- **Sisältö:** Kuukausinäkymäkalenteri vapaista varausajoista
- **Toiminnot:** Klikkaa aikaa → Varauslomake

#### `GET /Booking/Book/{id}` — Varauslomake
- **Pääsy:** Customer
- **Lomake:** Slot-tiedot näkyvillä, lisätiedot-kenttä
- **Toiminnot:** Varaa (POST) → sähköposti-ilmoitus

#### `GET /Booking/MyBookings` — Omat varaukset
- **Pääsy:** Customer
- **Sisältö:** Tulevat / menneet varaukset välilehdillä, status (Pending/Confirmed/Cancelled)
- **Toiminnot:** Peruuta varaus (POST `/Booking/Cancel/{id}`)

#### `GET /Booking/ManageSlots` — Admin: Varausaikojen hallinta
- **Pääsy:** Admin
- **Sisältö:** Kalenteri admin-näkymässä — slottien varausmäärät näkyvillä
- **Toiminnot:** Luo uusi varausaika, muokkaa, poista (vain jos ei aktiivisia varauksia)

#### `GET /Booking/ManageBookings` — Admin: Kaikki varaukset
- **Pääsy:** Admin
- **Sisältö:** Listanäkymä kaikista varauksista (suodatus statuksen ja asiakkaan mukaan)
- **Toiminnot:** Vahvista varaus (POST `/Booking/ConfirmBooking/{id}`) → sähköposti asiakkaalle

#### `GET /Booking/CreateSlot` / `GET /Booking/EditSlot/{id}` — Varausajan lomake
- **Pääsy:** Admin
- **Lomake:** Otsikko, kuvaus, tyyppi (Maintenance/Consulting/RemoteSupport), alkamisaika, kesto (min), maksimikapasiteetti

---

### Asiakkaat (Customer)

#### `GET /Customer` — Asiakasyrityslista
- **Pääsy:** Kaikki kirjautuneet
- **Sisältö:** Yrityksen nimi, Y-tunnus, sähköposti, puhelin, status (Aktiivinen/Ei)
- **Haku:** Nimi, Y-tunnus, sähköposti

#### `GET /Customer/{id}` — Asiakkaan tiedot
- **Pääsy:** Kaikki kirjautuneet
- **Sisältö:** Kaikki perustiedot + viimeiset 10 tikettiä, 10 laskua, sopimukset, käyttäjät

#### `GET /Customer/Create` / `GET /Customer/Edit/{id}` — Asiakaslomaket
- **Pääsy:** Admin
- **Lomake:** Yrityksen nimi, Y-tunnus (uniikki), sähköposti, puhelin, osoite

#### `POST /Customer/ToggleActive/{id}` — Aktivointi/deaktivointi
- **Pääsy:** Admin

#### `POST /Customer/Delete/{id}` — Poisto
- **Pääsy:** Admin
- **Rajoitus:** Vain jos ei tikettejä/laskuja/sopimuksia

---

### Yrityksen käyttäjät (CustomerUser)

#### `GET /CustomerUser` — Käyttäjälista
- **Pääsy:** Admin, CustomerAdmin
- **Sisältö:** Nimi, sähköposti, rooli (admin/käyttäjä), toiminnot
- **Huom:** CustomerAdmin näkee vain oman yrityksen käyttäjät

#### `GET /CustomerUser/Create` / `GET /CustomerUser/Edit/{id}` — Käyttäjälomakkeet
- **Pääsy:** Admin, CustomerAdmin
- **Lomake:** Koko nimi, sähköposti, salasana, yrityksen pääkäyttäjä (kyllä/ei)

---

### Palvelukatalogi (ServiceCatalog)

#### `GET /ServiceCatalog` — Palvelukatalogi (asiakasnäkymä)
- **Pääsy:** Kaikki kirjautuneet
- **Sisältö:** Palvelukortteja ryhmiteltyinä kategorioittain: nimi, kuvaus, hinta (tai "Sopimuksen mukaan")
- **Toiminnot:** Pyydä tarjous (POST `/ServiceCatalog/RequestQuote`) — Customer

#### `GET /ServiceCatalog/Manage` — Admin: Palveluiden hallinta
- **Pääsy:** Admin
- **Sisältö:** Taulukko kaikista palveluista (aktiiviset/ei-aktiiviset)
- **Toiminnot:** Luo, muokkaa, poista (= deaktivoi)

#### `GET /ServiceCatalog/Requests` — Tarjouspyynnöt
- **Pääsy:** Admin
- **Sisältö:** Kaikki tarjouspyynnöt, suodatus statuksen mukaan (Pending/InProgress/Sent/Accepted/Declined)
- **Toiminnot:** Avaa yksityiskohdat, päivitä status + admin-muistiinpanot

---

### Tietopankki (KnowledgeBase)

#### `GET /KnowledgeBase` — Tietopankin etusivu
- **Pääsy:** Kaikki kirjautuneet
- **Sisältö:** Hakukenttä, nostetut artikkelit, kategoriasuodatin
- **Toiminnot:** Hae artikkeleita (GET `/KnowledgeBase/Search` — JSON API)

#### `GET /KnowledgeBase/Article/{id}` — Artikkeli
- **Pääsy:** Kaikki kirjautuneet
- **Sisältö:** Artikkelin HTML-sisältö, kategoria, päiväys, katselukerrat, sivupalkki: aiheeseen liittyvät artikkelit
- **Huom:** Katselulaskuri kasvaa automaattisesti

#### `GET /KnowledgeBase/Manage` — Admin: Artikkelien hallinta
- **Pääsy:** Admin
- **Sisältö:** Kaikki artikkelit: otsikko, kategoria, julkaisustatus, nostettu, katselukerrat
- **Toiminnot:** Luo, muokkaa, poista

#### `GET /KnowledgeBase/Categories` — Admin: Kategoriat
- **Pääsy:** Admin
- **Sisältö:** Kategorioiden lista järjestysnumeroineen
- **Toiminnot:** Luo, muokkaa, poista (vain jos ei artikkeleita)

**Artikkeli- ja kategorialomakkeet:**
- Artikkeli: Otsikko, HTML-sisältö (WYSIWYG), kategoria, julkaistu (kyllä/ei), nostettu (kyllä/ei)
- Kategoria: Nimi, kuvaus, järjestysnumero, aktiivinen

---

### Etätyöpöytä (RemoteDesktop)

#### `GET /RemoteDesktop` — Yhteyskortit (Customer)
- **Pääsy:** Customer
- **Sisältö:** Kortit: nimi, protokolla (RDP/SSH/VNC), palvelin
- **Toiminnot:** Yhdistä → avaa Guacamole-iframe

#### `GET /RemoteDesktop/Connect/{id}` — Yhteys
- **Pääsy:** Customer
- **Sisältö:** Koko ruudun Guacamole-iframe etätyöpöydälle

#### `GET /RemoteDesktop/Manage` — Admin: Yhteyksien hallinta
- **Pääsy:** Admin
- **Sisältö:** Taulukko yhteyksistä: nimi, protokolla, palvelin, portti, asiakas, aktiivinen

#### `GET /RemoteDesktop/Create` / `GET /RemoteDesktop/Edit/{id}` — Yhteyslomaket
- **Pääsy:** Admin
- **Lomake:** Nimi, protokolla, palvelin, portti (oletus 3389), käyttäjätunnus, salasana (salataan IDataProtector), Ohita sertifikaattivirhe, Security-asetus, Guacamole-yhteys-ID, muistiinpanot, asiakas

---

### Tiedotteet (Announcement)

#### `GET /Announcement` — Tiedotteiden hallinta
- **Pääsy:** Admin
- **Sisältö:** Lista kaikista tiedotteista: otsikko, tyyppi, voimassaolo, julkaistu
- **Toiminnot:** Julkaise/piilota (POST `/Announcement/TogglePublish/{id}`), muokkaa, poista

#### `GET /Announcement/Create` / `GET /Announcement/Edit/{id}` — Tiedotelomake
- **Pääsy:** Admin
- **Lomake:** Otsikko, sisältö, tyyppi (Info / Warning / Maintenance), voimaantulo, voimassaolon loppu, julkaistu

> Tiedotteet näkyvät Dashboardin widget-osiossa kaikille kirjautuneille (kun julkaistu ja voimassaolo voimassa).

---

### Raportit (Report)

#### `GET /Report` — Raporttinäkymä
- **Pääsy:** Admin
- **Sisältö:**
  - Viikoittaiset tiketit: luotujen vs. ratkaistujen määrät (kaavio)
  - Kuukausittainen laskutus: laskujen määrä, summa ilman/sis. alv, maksettu (kaavio)
  - Tikettien statusjakauma
  - Palautteen keskiarvo
- **Toiminnot:**
  - Lataa tikettiraportti CSV (GET `/Report/ExportTicketsCsv`)
  - Lataa laskutusraportti CSV (GET `/Report/ExportBillingCsv`)
  - Lataa laskutusraportti PDF (GET `/Report/ExportBillingPdf`)

---

### Tapahtumaloki (AuditLog)

#### `GET /AuditLog` — Tapahtumaloki
- **Pääsy:** Admin
- **Sisältö:** Sivutettu (50/sivu) taulukko: aikaleima, käyttäjä, toiminto, kohde (tyyppi + ID), lisätiedot, IP
- **Suodattimet:** Toimintotyyppi, käyttäjä (sähköposti), kohteen tyyppi, päivämääräväli
- **Esimerkkitoiminnot:** Login, Logout, TicketCreated, TicketStatusChanged, InvoiceCreated, UserCreated

---

## Käyttöoikeusmatriisi

| Toiminto | Anon | Customer | CustomerAdmin | Admin |
|---------|:----:|:--------:|:-------------:|:-----:|
| Kirjaudu sisään | ✓ | — | — | — |
| Dashboard | — | ✓ | ✓ | ✓ |
| Luo tiketti | — | ✓ | ✓ | ✓ |
| Näe omat tiketit | — | ✓ | ✓ | ✓ |
| Näe kaikki tiketit | — | — | — | ✓ |
| Muuta tikettistatus/käsittelijä | — | — | — | ✓ |
| Lähetä tikettipalaute | — | ✓ | ✓ | — |
| Näe omat laskut | — | ✓ | ✓ | ✓ |
| Näe kaikki laskut | — | — | — | ✓ |
| Luo/muokkaa lasku | — | — | — | ✓ |
| Varaa aika | — | ✓ | ✓ | — |
| Hallinnoi varausaikoja | — | — | — | ✓ |
| Vahvista varauksia | — | — | — | ✓ |
| Näe asiakkaat | — | ✓ | ✓ | ✓ |
| Luo/muokkaa asiakkaita | — | — | — | ✓ |
| Hallinnoi yrityksen käyttäjiä | — | — | ✓ | ✓ |
| Pyydä tarjous | — | ✓ | ✓ | — |
| Hallinnoi palvelukatalogit | — | — | — | ✓ |
| Lue tietopankki | — | ✓ | ✓ | ✓ |
| Hallinnoi tietopankki | — | — | — | ✓ |
| Etätyöpöytäyhteys | — | ✓ | ✓ | ✓ |
| Hallinnoi etäyhteyksiä | — | — | — | ✓ |
| Näe tiedotteet (Dashboard) | — | ✓ | ✓ | ✓ |
| Luo/muokkaa tiedotteita | — | — | — | ✓ |
| Raportit | — | — | — | ✓ |
| Tapahtumaloki | — | — | — | ✓ |
| Vaihda oma salasana | — | ✓ | ✓ | ✓ |
| Luo uusi käyttäjä (rekisteröi) | — | — | — | ✓ |

---

## Lomakkeet ja tietovirrat

### Yleinen käytäntö

Kaikki lomakkeet käyttävät:
- `[ValidateAntiForgeryToken]` — CSRF-suojaus
- `ModelState`-validaatio palvelimella
- `TempData["SuccessMessage"]` / `TempData["ErrorMessage"]` — flash-viestit
- Onnistuminen → ohjataan Index- tai Details-sivulle

### Tiedostolataukset

- Maksimikoko: 20 MB
- Tallennetaan: `wwwroot/uploads/tickets/` tai `wwwroot/uploads/invoices/`
- Käsittelijä: `IFileStorageService.SaveFileAsync()`

### Sähköposti-ilmoitukset

Lähetetään automaattisesti seuraavissa tilanteissa:

| Tapahtuma | Vastaanottaja |
|-----------|---------------|
| Lasku luotu | Asiakkaan sähköposti |
| Tikettistatus muuttunut | Tiketin luoja |
| Varaus luotu | Järjestelmäilmoitus |
| Varaus vahvistettu | Varaaja |

---

## Teemat, värit ja responsiivisuus

### Väripaletti

| Käyttötarkoitus | Väri | Hex |
|----------------|------|-----|
| Pääväri (accent) | Sininen | `#2563eb` |
| Vaara / Poisto | Punainen | `#dc2626` |
| Varoitus | Oranssi | `#d97706` |
| Onnistuminen | Vihreä | `#16a34a` |
| Vaimeampi teksti | Harmaa | `#94a3b8` |
| Tausta (vaalea) | Harmainen | `#f1f5f9` |

### Tumma tila (Dark Mode)

- Tallennetaan: `localStorage` avaimella `hakatech-theme`
- Aktivoidaan: `data-bs-theme="dark"` Bootstrap-attribuutti `<html>`-elementissä
- CSS-muuttujat mukautuvat automaattisesti

### Statusmerkit

**Tikettistatus:**

| Status | Väri |
|--------|------|
| Open | Sininen |
| InProgress | Keltainen |
| WaitingCustomer | Oranssi |
| Resolved | Vihreä |
| Closed | Harmaa |

**Prioriteetti:**

| Prioriteetti | Väri |
|-------------|------|
| Low | Harmaa |
| Normal | Sininen |
| High | Oranssi |
| Critical | Punainen |

**Laskustatus:**

| Status | Väri |
|--------|------|
| Draft | Harmaa |
| Sent | Sininen |
| Unpaid | Oranssi |
| Paid | Vihreä |
| Overdue | Punainen |

### Responsiivisuus

| Leveys | Käyttäytyminen |
|--------|---------------|
| > 768 px | Sivupalkki kiinteä (256 px), pääsisältö scrollautuu |
| ≤ 768 px | Sivupalkki piilotetaan, yläreunaan ilmestyy hamburger-valikko |
| ≤ 480 px | Taulukot skrollaavat vaakasuunnassa, kortit pinoavat pystyssä |

---

## Lokalisaatio

| Kieli | Koodi | Valitaan |
|-------|-------|---------|
| Suomi | fi-FI | Oletus |
| Ruotsi | sv-SE | Kielivalitsimesta |
| Englanti | en-US | Kielivalitsimesta |

- Tallennustapa: evästepohjainen (`CookieRequestCultureProvider`)
- Vaihto: POST `/Account/SetLanguage` → sivu ladataan uudelleen
- Käännöstiedostot: `Resources/`-hakemistossa

---

## Näkymätiedostot

### Yhteensä 52 näkymää

| Hakemisto | Tiedostot | Lkm |
|-----------|-----------|-----|
| `Views/Shared/` | `_Layout.cshtml`, `_AuthLayout.cshtml`, `Error.cshtml`, `_ValidationScriptsPartial.cshtml` | 4 |
| `Views/Account/` | `Login`, `Register`, `ChangePassword`, `AccessDenied` | 4 |
| `Views/Home/` | `Index` (Dashboard), `Privacy` | 2 |
| `Views/Ticket/` | `Index`, `Details`, `Create` | 3 |
| `Views/Invoice/` | `Index`, `Details`, `Create` | 3 |
| `Views/Booking/` | `Index`, `Book`, `MyBookings`, `ManageSlots`, `SlotForm`, `ManageBookings` | 6 |
| `Views/Customer/` | `Index`, `Details`, `Create`, `Edit` | 4 |
| `Views/CustomerUser/` | `Index`, `Create`, `Edit` | 3 |
| `Views/ServiceCatalog/` | `Index`, `Manage`, `Create`, `Edit`, `Requests`, `RequestDetails` | 6 |
| `Views/KnowledgeBase/` | `Index`, `Article`, `Manage`, `ArticleForm`, `Categories`, `CategoryForm` | 6 |
| `Views/RemoteDesktop/` | `Index`, `Manage`, `Create`, `Edit`, `Connect` | 5 |
| `Views/Announcement/` | `Index`, `Create`, `Edit` | 3 |
| `Views/Report/` | `Index` | 1 |
| `Views/AuditLog/` | `Index` | 1 |
| `Views/` | `_ViewImports.cshtml`, `_ViewStart.cshtml` | 2 |
