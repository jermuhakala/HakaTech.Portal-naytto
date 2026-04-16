# HakaTech.Portal — Tietokantadokumentaatio

> Päivitetty: 2026-04-16  
> Tietokanta: SQL Server LocalDB (kehitys) — `HakaTechPortal`  
> ORM: Entity Framework Core 9 · Migraatioketju: 9 migraatiota

---

## Sisällysluettelo

1. [Yleiskatsaus](#yleiskatsaus)
2. [Taulurakenne](#taulurakenne)
3. [Taulut yksityiskohtaisesti](#taulut-yksityiskohtaisesti)
4. [Suhteet ja viite-eheyssäännöt](#suhteet-ja-viite-eheyssäännöt)
5. [Indeksit](#indeksit)
6. [Enumit](#enumit)
7. [Identity-taulut](#identity-taulut)
8. [Migraatioketju](#migraatioketju)
9. [Kehitysdata (SeedData)](#kehitysdata-seeddata)

---

## Yleiskatsaus

Portaali on B2B-asiakastukiportaali. Tietokannan keskeinen käsite on **Customer** (asiakasyritys), johon käyttäjät, tiketit, laskut, sopimukset ja muut resurssit liittyvät.

```
Customer (asiakasyritys)
├── ApplicationUser (käyttäjät)
├── Ticket (tukitiketit)
│   ├── TicketComment (kommentit)
│   ├── TicketAttachment (liitteet)
│   └── TicketFeedback (palaute, 1:1)
├── Invoice (laskut)
│   ├── InvoiceLine (laskurivit)
│   └── InvoiceAttachment (liitteet)
├── Contract (sopimukset)
├── Booking (varaukset)
├── QuoteRequest (tarjouspyynnöt)
└── RemoteDesktopConnection (etäyhteydet)
```

**Globaalit taulut** (ei Customer-sidottuja):
- `BookingSlot` — varausajat (admin luo)
- `ServiceCatalogItem` — palvelukatalogi
- `KnowledgeBaseCategory` + `KnowledgeBaseArticle` — tietopankki
- `Announcement` — tiedotteet
- `AuditLog` — tapahtumaloki

---

## Taulurakenne

| Taulu | Rivit (seed) | Kuvaus |
|-------|-------------|--------|
| `AspNetUsers` | 6 | Käyttäjät (Identity + laajennukset) |
| `Customers` | 3 | Asiakasyritykset |
| `Tickets` | 5 | Tukitiketit |
| `TicketComments` | 5 | Tikettien kommentit |
| `TicketAttachments` | 0 | Tikettien liitetiedostot |
| `TicketFeedbacks` | 1 | Tikettipalautteet (1/tiketti) |
| `Invoices` | 4 | Laskut |
| `InvoiceLines` | 6 | Laskurivit |
| `InvoiceAttachments` | 0 | Laskujen liitetiedostot |
| `Contracts` | 3 | Palvelusopimukset |
| `ServiceCatalogItems` | 6 | Palvelukatalogituotteet |
| `QuoteRequests` | 2 | Tarjouspyynnöt |
| `Announcements` | 2 | Tiedotteet |
| `RemoteDesktopConnections` | 1 | Etäyhteysasetukset |
| `KnowledgeBaseCategories` | 4 | Tietopankin kategoriat |
| `KnowledgeBaseArticles` | 6 | Tietopankin artikkelit |
| `AuditLogs` | — | Tapahtumaloki |
| `BookingSlots` | 6 | Varausajat |
| `Bookings` | 3 | Varaukset |

---

## Taulut yksityiskohtaisesti

### `Customers`

Asiakasyritys — koko järjestelmän keskeinen entiteetti.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK, IDENTITY | Pääavain |
| `CompanyName` | nvarchar(max) | NOT NULL | Yrityksen nimi |
| `BusinessId` | nvarchar(max) | NOT NULL, UNIQUE | Y-tunnus |
| `ContactEmail` | nvarchar(max) | NOT NULL | Yhteystietosähköposti |
| `Phone` | nvarchar(max) | NULL | Puhelinnumero |
| `Address` | nvarchar(max) | NULL | Osoite |
| `CreatedAt` | datetime2 | NOT NULL, DEFAULT UtcNow | Luontiaika |
| `IsActive` | bit | NOT NULL, DEFAULT 1 | Aktiivinen asiakas |

---

### `AspNetUsers` (ApplicationUser)

Laajentaa ASP.NET Core Identity -käyttäjää.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| *(Identity-kentät)* | — | — | Id, UserName, Email, PasswordHash, jne. |
| `FullName` | nvarchar(max) | NOT NULL | Käyttäjän koko nimi |
| `CustomerId` | int | NULL, FK → Customers | Null = admin-käyttäjä |
| `IsCustomerAdmin` | bit | NOT NULL, DEFAULT 0 | Yrityksen pääkäyttäjä |
| `DashboardLayout` | nvarchar(max) | NULL | Pilkuilla eroteltu widget-avainlista |

> **Huom:** Null `CustomerId` tarkoittaa admin-käyttäjää. Customer-käyttäjillä on aina viittaus asiakasyritykseen.

---

### `Tickets`

Tukitiketit — asiakkaan luomat tukipyynnöt.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `Title` | nvarchar(300) | NOT NULL | Otsikko |
| `Description` | nvarchar(max) | NOT NULL | Kuvaus |
| `Status` | int | NOT NULL | Enum: TicketStatus |
| `Priority` | int | NOT NULL | Enum: TicketPriority |
| `Category` | int | NOT NULL | Enum: TicketCategory |
| `CreatedAt` | datetime2 | NOT NULL | Luontiaika |
| `UpdatedAt` | datetime2 | NOT NULL | Muokkausaika |
| `ResolvedAt` | datetime2 | NULL | Ratkaisuaika |
| `CreatedByUserId` | nvarchar(450) | NOT NULL, FK → AspNetUsers | Luoja |
| `AssignedToUserId` | nvarchar(450) | NULL, FK → AspNetUsers | Käsittelijä (admin) |
| `CustomerId` | int | NOT NULL, FK → Customers | Asiakasyritys |

---

### `TicketComments`

Tikettien kommentit — sekä julkiset että sisäiset admin-muistiinpanot.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `Content` | nvarchar(max) | NOT NULL | Kommentin sisältö |
| `IsInternal` | bit | NOT NULL, DEFAULT 0 | Vain admin näkee |
| `CreatedAt` | datetime2 | NOT NULL | |
| `TicketId` | int | NOT NULL, FK → Tickets | |
| `AuthorId` | nvarchar(450) | NOT NULL, FK → AspNetUsers | |

---

### `TicketAttachments`

Tiketteihin ladatut liitetiedostot.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `FileName` | nvarchar(max) | NOT NULL | Alkuperäinen tiedostonimi |
| `FilePath` | nvarchar(max) | NOT NULL | Polku: `wwwroot/uploads/` |
| `UploadedAt` | datetime2 | NOT NULL | |
| `UploadedByUserId` | nvarchar(450) | NOT NULL, FK → AspNetUsers | |
| `TicketId` | int | NOT NULL, FK → Tickets | |

---

### `TicketFeedbacks`

Asiakaspalaute ratkaistuille tiketeille — yksi per tiketti.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `TicketId` | int | NOT NULL, FK → Tickets, UNIQUE | Yksi palaute/tiketti |
| `UserId` | nvarchar(450) | NOT NULL, FK → AspNetUsers | |
| `Rating` | int | NOT NULL | Arvosana 1–5 |
| `Comment` | nvarchar(2000) | NULL | Vapaa kommentti |
| `SubmittedAt` | datetime2 | NOT NULL | |

---

### `Invoices`

Laskut asiakasyrityksille.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `InvoiceNumber` | nvarchar(50) | NOT NULL, UNIQUE | Laskunnumero |
| `Status` | int | NOT NULL | Enum: InvoiceStatus |
| `InvoiceDate` | datetime2 | NOT NULL | Laskun päiväys |
| `DueDate` | datetime2 | NOT NULL | Eräpäivä |
| `PaidAt` | datetime2 | NULL | Maksuaika |
| `Notes` | nvarchar(max) | NULL | Lisätiedot |
| `VatRate` | decimal(5,4) | NOT NULL, DEFAULT 0.255 | ALV-kanta (25,5 %) |
| `CustomerId` | int | NOT NULL, FK → Customers | |

> **Lasketut arvot** (C#-tasolla, ei tietokannassa):
> - `SubTotal` = Σ(Lines: Quantity × UnitPrice)
> - `VatAmount` = SubTotal × VatRate
> - `TotalAmount` = SubTotal + VatAmount

---

### `InvoiceLines`

Laskurivit laskuille.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `Description` | nvarchar(max) | NOT NULL | Rivin kuvaus |
| `Quantity` | decimal(10,2) | NOT NULL, DEFAULT 1 | Määrä |
| `UnitPrice` | decimal(18,2) | NOT NULL | Yksikköhinta (€, alv 0) |
| `InvoiceId` | int | NOT NULL, FK → Invoices | |

---

### `InvoiceAttachments`

Laskuihin ladatut liitteet.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `FileName` | nvarchar(max) | NOT NULL | |
| `FilePath` | nvarchar(max) | NOT NULL | Polku: `wwwroot/uploads/` |
| `UploadedAt` | datetime2 | NOT NULL | |
| `UploadedByUserId` | nvarchar(450) | NOT NULL, FK → AspNetUsers | |
| `InvoiceId` | int | NOT NULL, FK → Invoices | |

---

### `Contracts`

Palvelusopimukset asiakasyrityksille.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `Type` | int | NOT NULL | Enum: ContractType |
| `Description` | nvarchar(max) | NOT NULL | Sopimusehtojen kuvaus |
| `StartDate` | datetime2 | NOT NULL | Alkamispäivä |
| `EndDate` | datetime2 | NOT NULL | Päättymispäivä |
| `TicketQuota` | int | NOT NULL, DEFAULT 30 | Kuukausikiintiö (tikettiä/kk) |
| `TicketsUsed` | int | NOT NULL, DEFAULT 0 | Käytetyt tiketit kuussa |
| `MonthlyPrice` | decimal(18,2) | NOT NULL | Kuukausihinta (€) |
| `IsActive` | bool | NOT NULL, DEFAULT 1 | Sopimus voimassa |
| `CustomerId` | int | NOT NULL, FK → Customers | |

---

### `ServiceCatalogItems`

Palvelukatalogituotteet tarjouspyyntöjä varten.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `Name` | nvarchar(max) | NOT NULL | Palvelun nimi |
| `Description` | nvarchar(max) | NOT NULL | Kuvaus |
| `Category` | nvarchar(100) | NULL | Kategoria (esim. "Tietoturva") |
| `Price` | decimal(18,2) | NULL | Aloitushinta (€), null = sopimuksen mukaan |
| `IsActive` | bit | NOT NULL, DEFAULT 1 | Näytetään katalogissa |
| `CreatedAt` | datetime2 | NOT NULL | |

---

### `QuoteRequests`

Asiakkaan lähettämät tarjouspyynnöt palvelukatalogituotteille.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `ServiceCatalogItemId` | int | NOT NULL, FK → ServiceCatalogItems | |
| `CustomerId` | int | NOT NULL, FK → Customers | |
| `CreatedByUserId` | nvarchar(450) | NOT NULL, FK → AspNetUsers | |
| `Message` | nvarchar(max) | NULL | Asiakkaan viesti |
| `AdminNotes` | nvarchar(max) | NULL | Admin-muistiinpanot |
| `Status` | int | NOT NULL | Enum: QuoteRequestStatus |
| `CreatedAt` | datetime2 | NOT NULL | |
| `UpdatedAt` | datetime2 | NOT NULL | |

---

### `Announcements`

Hallinnolliset tiedotteet, jotka näkyvät portaalissa.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `Title` | nvarchar(max) | NOT NULL | Otsikko |
| `Content` | nvarchar(max) | NOT NULL | Sisältö |
| `Type` | int | NOT NULL | Enum: AnnouncementType |
| `ValidFrom` | datetime2 | NULL | Näyttöaika alkaa |
| `ValidUntil` | datetime2 | NULL | Näyttöaika päättyy |
| `IsPublished` | bit | NOT NULL, DEFAULT 1 | Julkaistu |
| `CreatedByUserId` | nvarchar(450) | NOT NULL, FK → AspNetUsers | |
| `CreatedAt` | datetime2 | NOT NULL | |

---

### `RemoteDesktopConnections`

Asiakkaiden etäyhteysasetukset (Apache Guacamole -integraatio).

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `Name` | nvarchar(200) | NOT NULL | Yhteysnimi |
| `Protocol` | int | NOT NULL | Enum: RemoteDesktopProtocol |
| `Hostname` | nvarchar(500) | NOT NULL | Palvelimen osoite |
| `Port` | int | NOT NULL, DEFAULT 3389 | Portti |
| `Username` | nvarchar(200) | NULL | Käyttäjätunnus |
| `EncryptedPassword` | nvarchar(2000) | NULL | Salattu salasana (IDataProtector) |
| `IgnoreCert` | bit | NOT NULL, DEFAULT 1 | Ohita sertifikaattivirhe |
| `Security` | nvarchar(50) | NOT NULL, DEFAULT "any" | Guacamole security-asetus |
| `GuacamoleConnectionId` | nvarchar(max) | NULL | Guacamolen yhteys-ID |
| `Notes` | nvarchar(2000) | NULL | Muistiinpanot |
| `IsActive` | bit | NOT NULL, DEFAULT 1 | Yhteys käytössä |
| `CreatedAt` | datetime2 | NOT NULL | |
| `CustomerId` | int | NOT NULL, FK → Customers | |

---

### `KnowledgeBaseCategories`

Tietopankin artikkeleiden kategoriat.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `Name` | nvarchar(100) | NOT NULL | Kategorian nimi |
| `Description` | nvarchar(500) | NULL | Kuvaus |
| `SortOrder` | int | NOT NULL | Järjestysnumero |
| `IsActive` | bit | NOT NULL, DEFAULT 1 | Aktiivinen |

---

### `KnowledgeBaseArticles`

Tietopankin artikkelit (HTML-muotoinen sisältö).

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `Title` | nvarchar(300) | NOT NULL | Otsikko |
| `Content` | nvarchar(max) | NOT NULL | Sisältö (HTML) |
| `CategoryId` | int | NOT NULL, FK → KnowledgeBaseCategories | |
| `IsPublished` | bit | NOT NULL | Julkaistu |
| `IsFeatured` | bit | NOT NULL | Nostettu etusivulle |
| `ViewCount` | int | NOT NULL, DEFAULT 0 | Katselukerrat |
| `CreatedAt` | datetime2 | NOT NULL | |
| `UpdatedAt` | datetime2 | NOT NULL | |
| `CreatedByUserId` | nvarchar(450) | NOT NULL, FK → AspNetUsers | |

---

### `AuditLogs`

Tapahtumaloki käyttäjien toiminnoista. Tarkoituksella denormalisoitu — säilyttää tiedot vaikka käyttäjä poistuisi.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `Timestamp` | datetime2 | NOT NULL, INDEX | Aikaleima |
| `UserId` | nvarchar(max) | NULL, INDEX | Käyttäjän ID (denormalisoitu) |
| `UserEmail` | nvarchar(max) | NULL | Sähköposti (denormalisoitu) |
| `Action` | nvarchar(max) | NOT NULL | Toiminto (esim. "Login", "TicketCreated") |
| `EntityType` | nvarchar(max) | NULL | Kohteen tyyppi (esim. "Ticket") |
| `EntityId` | nvarchar(max) | NULL | Kohteen ID |
| `Details` | nvarchar(max) | NULL | Lisätiedot |
| `IpAddress` | nvarchar(max) | NULL | IP-osoite |

---

### `BookingSlots`

Admin luo varattavissa olevat ajat.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `Title` | nvarchar(max) | NOT NULL | Otsikko |
| `Description` | nvarchar(max) | NOT NULL | Kuvaus |
| `SlotType` | int | NOT NULL | Enum: BookingSlotType |
| `StartTime` | datetime2 | NOT NULL | Alkamisaika (paikallinen aika) |
| `DurationMinutes` | int | NOT NULL, DEFAULT 60 | Kesto minuutteina |
| `MaxCapacity` | int | NOT NULL, DEFAULT 1 | Maksimimäärä varauksia |
| `IsActive` | bit | NOT NULL, DEFAULT 1 | Varattavissa |
| `CreatedByUserId` | nvarchar(450) | NULL, FK → AspNetUsers | |

> **Lasketut ominaisuudet** (C#):  
> `EndTime`, `ActiveBookingsCount`, `AvailableSpots`, `IsFull`, `IsPast`, `IsAvailable`

---

### `Bookings`

Asiakkaiden tekemät varaukset.

| Sarake | Tyyppi | Rajoitteet | Kuvaus |
|--------|--------|------------|--------|
| `Id` | int | PK | |
| `BookingSlotId` | int | NOT NULL, FK → BookingSlots | |
| `CustomerId` | int | NOT NULL, FK → Customers | |
| `UserId` | nvarchar(450) | NOT NULL, FK → AspNetUsers | |
| `Notes` | nvarchar(max) | NULL | Lisätiedot |
| `Status` | int | NOT NULL | Enum: BookingStatus |
| `CreatedAt` | datetime2 | NOT NULL | |
| `CancelledAt` | datetime2 | NULL | Peruutusaika |
| `CancellationReason` | nvarchar(max) | NULL | Peruutuksen syy |

---

## Suhteet ja viite-eheyssäännöt

| Lapsi-taulu | FK | Vanhempi | Poistoehto |
|-------------|-----|----------|------------|
| `AspNetUsers.CustomerId` | → `Customers.Id` | Customer | **SetNull** |
| `Tickets.CustomerId` | → `Customers.Id` | Customer | **Cascade** |
| `Tickets.CreatedByUserId` | → `AspNetUsers.Id` | User | **Restrict** |
| `Tickets.AssignedToUserId` | → `AspNetUsers.Id` | User | **SetNull** |
| `TicketComments.TicketId` | → `Tickets.Id` | Ticket | **Cascade** |
| `TicketComments.AuthorId` | → `AspNetUsers.Id` | User | **Restrict** |
| `TicketAttachments.TicketId` | → `Tickets.Id` | Ticket | **Cascade** |
| `TicketAttachments.UploadedByUserId` | → `AspNetUsers.Id` | User | **Restrict** |
| `TicketFeedbacks.TicketId` | → `Tickets.Id` | Ticket | **Cascade** |
| `TicketFeedbacks.UserId` | → `AspNetUsers.Id` | User | **Restrict** |
| `Invoices.CustomerId` | → `Customers.Id` | Customer | **Restrict** |
| `InvoiceLines.InvoiceId` | → `Invoices.Id` | Invoice | **Cascade** |
| `InvoiceAttachments.InvoiceId` | → `Invoices.Id` | Invoice | **Cascade** |
| `InvoiceAttachments.UploadedByUserId` | → `AspNetUsers.Id` | User | **Restrict** |
| `Contracts.CustomerId` | → `Customers.Id` | Customer | **Cascade** |
| `QuoteRequests.ServiceCatalogItemId` | → `ServiceCatalogItems.Id` | Service | **Restrict** |
| `QuoteRequests.CustomerId` | → `Customers.Id` | Customer | **Cascade** |
| `QuoteRequests.CreatedByUserId` | → `AspNetUsers.Id` | User | **Restrict** |
| `Announcements.CreatedByUserId` | → `AspNetUsers.Id` | User | **Restrict** |
| `RemoteDesktopConnections.CustomerId` | → `Customers.Id` | Customer | **Cascade** |
| `KnowledgeBaseArticles.CategoryId` | → `KnowledgeBaseCategories.Id` | Category | **Restrict** |
| `KnowledgeBaseArticles.CreatedByUserId` | → `AspNetUsers.Id` | User | **Restrict** |
| `Bookings.BookingSlotId` | → `BookingSlots.Id` | Slot | **Cascade** |
| `Bookings.CustomerId` | → `Customers.Id` | Customer | **Restrict** |
| `Bookings.UserId` | → `AspNetUsers.Id` | User | **Restrict** |
| `BookingSlots.CreatedByUserId` | → `AspNetUsers.Id` | User | **SetNull** |

---

## Indeksit

| Taulu | Sarake(et) | Tyyppi | Tarkoitus |
|-------|-----------|--------|-----------|
| `Customers` | `BusinessId` | UNIQUE | Y-tunnus-yksikäisyys |
| `Invoices` | `InvoiceNumber` | UNIQUE | Laskunnumeron yksikäisyys |
| `TicketFeedbacks` | `TicketId` | UNIQUE | Max 1 palaute/tiketti |
| `AuditLogs` | `Timestamp` | INDEX | Aikavälikyselyt |
| `AuditLogs` | `UserId` | INDEX | Käyttäjäkohtaiset kyselyt |

---

## Enumit

### `TicketStatus`
| Arvo | Nimi | Merkitys |
|------|------|----------|
| 0 | Open | Avoin |
| 1 | InProgress | Käsittelyssä |
| 2 | WaitingCustomer | Odottaa asiakasta |
| 3 | Resolved | Ratkaistu |
| 4 | Closed | Suljettu |

### `TicketPriority`
| Arvo | Nimi |
|------|------|
| 0 | Low |
| 1 | Normal |
| 2 | High |
| 3 | Critical |

### `TicketCategory`
| Arvo | Nimi |
|------|------|
| 0 | Network |
| 1 | Hardware |
| 2 | Software |
| 3 | Email |
| 4 | Access |
| 5 | Other |

### `InvoiceStatus`
| Arvo | Nimi | Merkitys |
|------|------|----------|
| 0 | Draft | Luonnos |
| 1 | Sent | Lähetetty |
| 2 | Unpaid | Maksamatta |
| 3 | Paid | Maksettu |
| 4 | Overdue | Erääntynyt |

### `ContractType`
| Arvo | Nimi | Kuvaus |
|------|------|--------|
| 0 | Support24_7 | 24/7-tuki |
| 1 | SupportBusiness | Toimistoaikatuki |
| 2 | Managed | Hallittu palvelu |
| 3 | OneTime | Kertatyö |

### `BookingStatus`
| Arvo | Nimi |
|------|------|
| 0 | Pending |
| 1 | Confirmed |
| 2 | Cancelled |

### `BookingSlotType`
| Arvo | Nimi |
|------|------|
| 0 | Maintenance |
| 1 | Consulting |
| 2 | RemoteSupport |

### `AnnouncementType`
| Arvo | Nimi |
|------|------|
| 0 | Info |
| 1 | Maintenance |
| 2 | Warning |

### `QuoteRequestStatus`
| Arvo | Nimi |
|------|------|
| 0 | Pending |
| 1 | InProgress |
| 2 | Sent |
| 3 | Accepted |
| 4 | Declined |

### `RemoteDesktopProtocol`
| Arvo | Nimi |
|------|------|
| 0 | Rdp |
| 1 | Vnc |
| 2 | Ssh |

---

## Identity-taulut

ASP.NET Core Identity luo automaattisesti seuraavat taulut:

| Taulu | Kuvaus |
|-------|--------|
| `AspNetUsers` | Käyttäjät (laajennettu ApplicationUser-luokalla) |
| `AspNetRoles` | Roolit: `Admin`, `Customer` |
| `AspNetUserRoles` | Käyttäjä–rooli-liitokset |
| `AspNetUserClaims` | Käyttäjäkohtaiset claimsit |
| `AspNetRoleClaims` | Roolikohtaiset claimsit |
| `AspNetUserLogins` | Ulkoiset kirjautumiset (OAuth) |
| `AspNetUserTokens` | Tokenien tallennus |

---

## Migraatioketju

| # | Migraatio | Päivämäärä | Lisätyt taulut / sarakkeet |
|---|-----------|------------|---------------------------|
| 1 | `InitialCreate` | 2026-04-06 | Customers, AspNetUsers, Tickets, TicketComments, Invoices, InvoiceLines, Contracts, ServiceCatalogItems, QuoteRequests |
| 2 | `AddAttachments` | 2026-04-10 | TicketAttachments, InvoiceAttachments |
| 3 | `AddServiceCatalogAndAnnouncements` | 2026-04-10 | Announcements (täydentää ServiceCatalog-integraatiota) |
| 4 | `AddRemoteDesktopConnections` | 2026-04-13 | RemoteDesktopConnections |
| 5 | `AddGuacamoleConnectionId` | 2026-04-15 | `RemoteDesktopConnections.GuacamoleConnectionId` |
| 6 | `AddKnowledgeBase` | 2026-04-15 | KnowledgeBaseCategories, KnowledgeBaseArticles |
| 7 | `AddFeedbackAuditCustomerAdmin` | 2026-04-15 | TicketFeedbacks, AuditLogs, `AspNetUsers.IsCustomerAdmin` |
| 8 | `AddBookingCalendar` | 2026-04-15 | BookingSlots, Bookings |
| 9 | `AddDashboardLayoutAndLocalization` | 2026-04-15 | `AspNetUsers.DashboardLayout` |

---

## Kehitysdata (SeedData)

> **Huom:** `SeedData` pudottaa ja luo tietokannan uudelleen joka käynnistyksen yhteydessä (kehitysympäristö). Älä luota pysyvään dataan kehityksessä.

### Käyttäjät

| Sähköposti | Rooli | Asiakas | IsCustomerAdmin |
|-----------|-------|---------|-----------------|
| admin@hakatech.fi | Admin | — | — |
| support@hakatech.fi | Admin | — | — |
| matti@digimolli.fi | Customer | DigiMölli Oy | Kyllä |
| laura@digimolli.fi | Customer | DigiMölli Oy | Ei |
| kalle@kivikangas.fi | Customer | Kivikangas Rakennus | Kyllä |
| miia@techsolutions.fi | Customer | TechSolutions Finland | Kyllä |

### Asiakkaat

| Yritys | Y-tunnus | Sopimus | Hinta/kk |
|--------|---------|---------|----------|
| DigiMölli Oy | 1234567-8 | SupportBusiness | 450 € |
| Kivikangas Rakennus | 9876543-2 | OneTime | 0 € |
| TechSolutions Finland | 1122334-5 | Support24_7 | 1 200 € |

### Tietopankin kategoriat (seed)

- Yleistä
- Laskutus
- Tekniset ohjeet
- Tietoturva
