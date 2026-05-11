// Nimiavaruus kertoo missä kohtaa projektin rakennetta tämä tiedosto sijaitsee.
// "Domain" = sovelluksen ydinliiketoimintalogiikka — ei UI:ta, ei tietokantakoodia, vain data.
namespace HakaTech.Portal.Models.Domain;

/// <summary>Tiketin elinkaaren tilat: avoin → käsittelyssä → ratkaistu/suljettu.</summary>
// Enum (luettelotyyppi) = nimettyjen vakioiden joukko. Konepellin alla nämä ovat kokonaislukuja:
// Open=0, InProgress=1, jne. Tietokantaan tallentuu numero, mutta koodissa käytetään luettavia nimiä.
public enum TicketStatus
{
    Open,            // Arvo 0 — Avoin: uusi tiketti, ei vielä otettu käsittelyyn.
    InProgress,      // Arvo 1 — Käsittelyssä: joku admin on ottanut tiketin työn alle.
    WaitingCustomer, // Arvo 2 — Odottaa asiakasta: admin on pyytänyt lisätietoja.
    Resolved,        // Arvo 3 — Ratkaistu: admin katsoo työn valmiiksi, odottaa asiakkaan vahvistusta.
    Closed           // Arvo 4 — Suljettu: prosessi päättynyt, tiketti arkistoitu.
}

/// <summary>Tiketin kiireellisyys. Vaikuttaa SLA-vasteaikoihin.</summary>
// SLA = Service Level Agreement: sopimuksessa luvattu vasteaika eri prioriteeteille.
public enum TicketPriority
{
    Low,      // Arvo 0 — Matala: ei kiireellinen, voidaan hoitaa lähipäivinä.
    Normal,   // Arvo 1 — Normaali: perusprioriteetti, hoidetaan järjestyksessä.
    High,     // Arvo 2 — Korkea: kiireellinen, vaatii nopean reagoinnin.
    Critical  // Arvo 3 — Kriittinen: liiketoiminta pysähtynyt, vaatii välitöntä toimintaa.
}

/// <summary>Tiketin aihealue. Helpottaa tikettien jakamista oikealle tiimille.</summary>
// Kategorioiden avulla admin voi suodattaa ja ohjata tikettejä eri asiantuntijoille.
public enum TicketCategory
{
    Network,   // Verkko: palomuurit, VPN-yhteydet, WLAN-ongelmat.
    Hardware,  // Laitteet: tulostimet, työasemat, palvelimet, näytöt.
    Software,  // Ohjelmistot: sovellukset, lisenssit, päivitykset.
    Email,     // Sähköposti: Microsoft 365, postilaatikko-ongelmat.
    Access,    // Käyttöoikeudet: salasanat, Active Directory, kaksivaiheinen tunnistus.
    Other      // Muut: kaikki mikä ei sovi yllä oleviin kategorioihin.
}

/// <summary>
/// Tukipyyntö (tiketti). Asiakkaan käyttäjä luo tiketin, admin ottaa sen
/// käsittelyyn ja kommunikointi tapahtuu kommenttien kautta.
/// </summary>
// "public class Ticket" = julkinen luokka nimeltä Ticket.
// Luokka on "malli" (template): jokaisesta tikettitietueesta tietokannassa
// muodostuu yksi Ticket-olio muistiin.
public class Ticket
{
    // Id on pääavain. EF Core tunnistaa nimen "Id" automaattisesti ja tekee siitä
    // tietokannan PRIMARY KEY -sarakkeen, joka kasvaa automaattisesti (IDENTITY).
    public int Id { get; set; }

    /// <summary>Tiketin lyhyt otsikko, näkyy listoissa.</summary>
    // string = tekstityyppinen kenttä. "= string.Empty" asettaa oletusarvoksi tyhjän merkkijonon
    // tyhjän merkkijonon sijaan null-arvosta, mikä tekee koodista turvallisempaa
    // (ei tarvitse aina tarkistaa onko arvo null ennen käyttöä).
    public string Title       { get; set; } = string.Empty;

    /// <summary>Pidempi vapaa kuvaus ongelmasta.</summary>
    // Kuvauskentässä asiakas kertoo ongelmansa tarkemmin.
    public string Description { get; set; } = string.Empty;

    // Alla olevan kolmen ominaisuuden tyyppi on enum (ks. yllä).
    // EF Core tallentaa enumin tietokantaan kokonaislukuna.

    // Status: tiketin nykyinen elinkaaren vaihe. Oletusarvo Open = uusi tiketti on avoin.
    public TicketStatus   Status   { get; set; } = TicketStatus.Open;

    // Priority: kuinka kiireellisesti tiketti pitää hoitaa. Oletus: normaali.
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    // Category: mille osa-alueelle tiketti kuuluu. Oletus: Other.
    public TicketCategory Category { get; set; } = TicketCategory.Other;

    /// <summary>Milloin tiketti luotiin (UTC).</summary>
    // DateTime = päivämäärä ja kellonaika. UTC (Coordinated Universal Time) = maailmanaika.
    // Kaikki aikaleimat tallennetaan UTC:nä → ei aikavyöhykesekaannuksia.
    // DateTime.UtcNow = nykyinen UTC-aika automaattisesti oletuksena.
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    /// <summary>Milloin tikettiä on viimeksi muokattu (UTC).</summary>
    // Päivitetään aina kun tiketin tilaa, kommenttia tai muuta muutetaan.
    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;

    /// <summary>Milloin tiketti merkittiin ratkaistuksi. null jos vielä avoin.</summary>
    // "DateTime?" = nullable DateTime. Kysymysmerkki tarkoittaa "voi olla myös null".
    // null = tikettiä ei vielä ratkaistu. Arvo asetetaan kun status → Resolved.
    public DateTime? ResolvedAt{ get; set; }

    // ── Tiketin luoja (asiakkaan käyttäjä) ────────────────────────────────────
    // Yhteys ApplicationUser-tauluun: yksi käyttäjä voi luoda monta tikettiä.
    // EF Core osaa yhdistää nämä automaattisesti SQL:n JOIN-kyselyksi.

    // CreatedByUserId = viiteavain (foreign key) — säilyttää käyttäjän ID:n merkkijonona.
    // ASP.NET Identity käyttää string-tyyppisiä GUID-tunnuksia käyttäjätunnuksina.
    public string CreatedByUserId { get; set; } = string.Empty;

    // CreatedByUser = navigaatio-ominaisuus. EF Core täyttää tämän automaattisesti
    // kun hakukyselyyn lisätään .Include(t => t.CreatedByUser).
    // "?" = nullable: voidaan jättää lataamatta jos ei tarvita.
    public ApplicationUser? CreatedByUser { get; set; }

    // ── Admin-vastuuhenkilö ────────────────────────────────────────────────────
    // Nullable koska tiketti ei heti saa vastuuhenkilöä — admin ottaa sen työn alle myöhemmin.

    // Nullable string: jos null = tikettiä ei ole vielä osoitettu kenellekään.
    public string? AssignedToUserId { get; set; }

    // Navigaatio vastuuhenkilön tietoihin. Null jos ei osoitettu.
    public ApplicationUser? AssignedToUser { get; set; }

    // ── Mille asiakasyritykselle tiketti kuuluu ────────────────────────────────
    // Yksi Customer voi omistaa monta tikettiä (one-to-many suhde).

    // CustomerId = viiteavain Customer-tauluun. Pakollinen (ei nullable) → tiketti
    // täytyy aina kuulua jollekin yritykselle.
    public int CustomerId { get; set; }

    // Navigaatio asiakasyritykseen.
    public Customer? Customer { get; set; }

    /// <summary>Tiketin kommentit (keskustelu asiakkaan ja admin välillä).</summary>
    // ICollection<T> = joukko kohteita — käytännössä lista kommenteista.
    // "= new List<TicketComment>()" = alustetaan tyhjäksi listaksi,
    // jotta koodissa ei tarvitse tarkistaa null:ia ennen foreach-silmukkaa.
    public ICollection<TicketComment>    Comments    { get; set; } = new List<TicketComment>();

    /// <summary>Tikettiin liitetyt tiedostot (kuvakaappaukset, lokit jne.).</summary>
    // Sama periaate: tyhjä lista oletuksena.
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();

    /// <summary>Asiakkaan jättämä palaute, kun tiketti on suljettu (1–5 tähteä).</summary>
    // Nullable: palaute luodaan vasta kun asiakas lähettää sen. Null = ei vielä palautetta.
    // Yksi tiketti → max yksi palaute (one-to-one suhde).
    public TicketFeedback? Feedback { get; set; }
}
