// Nimiavaruus — tämä tiedosto kuuluu domainmallien ryhmään.
namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Asiakasyritys. Yksi asiakas voi sisältää useita käyttäjiä, tikettejä,
/// laskuja ja sopimuksia.
/// </summary>
// Luokka edustaa yhtä riviä tietokannan Customers-taulussa.
// EF Core luo taulun automaattisesti tämän luokan perusteella (Code First -malli).
public class Customer
{
    /// <summary>Tietokannan pääavain (juokseva numero).</summary>
    // EF Core tunnistaa "Id"-nimen automaattisesti pääavaimeksi.
    // Tietokanta kasvattaa tätä numeroa automaattisesti joka INSERT-kyselyssä.
    public int Id { get; set; }

    /// <summary>Yrityksen virallinen nimi.</summary>
    // Pakollinen kenttä — ei voi olla tyhjä (validoidaan ViewModelissa [Required]-attribuutilla).
    // Oletusarvo string.Empty estää null-virheet koodissa.
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Y-tunnus — yksilöllinen tunniste suomalaisille yrityksille.</summary>
    // Muoto: 7 numeroa + väliviiva + tarkistusnumero, esim. "1234567-8".
    // Validointi tehdään ViewModelissa RegularExpression-attribuutilla.
    // Tietokannassa tällä on UNIQUE-rajoite: sama Y-tunnus ei voi esiintyä kahdella yrityksellä.
    public string BusinessId   { get; set; } = string.Empty;

    /// <summary>Yrityksen yleinen yhteyssähköposti (lasku-/tukiviestintä).</summary>
    // Tähän osoitteeseen lähetetään automaattiset ilmoitukset (uudet laskut jne.).
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>Puhelinnumero (vapaaehtoinen).</summary>
    // "string?" = nullable string. Kysymysmerkki tarkoittaa: voi olla null (ei pakollinen).
    public string? Phone       { get; set; }

    /// <summary>Käyntiosoite (vapaaehtoinen).</summary>
    // Nullable — kaikilla asiakkailla ei ole tarvetta tallentaa osoitetta.
    public string? Address     { get; set; }

    /// <summary>Milloin asiakas on lisätty järjestelmään (UTC).</summary>
    // DateTime.UtcNow = nykyinen aika koordinoituna maailmanaikana oletuksena.
    // UTC varmistaa että aika on oikein riippumatta palvelimen sijaintimaasta.
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    /// <summary>Onko asiakkuus aktiivinen. Inaktiivisia ei näytetä yleisissä listoissa.</summary>
    // bool = totuusarvo: true tai false. Oletuksena true = uudet asiakkaat ovat heti aktiivisia.
    // Käytämme "soft delete" -periaatetta: asiakas ei koskaan poistu oikeasti tietokannasta,
    // vaan merkitään vain ei-aktiiviseksi. Näin historia säilyy.
    public bool IsActive       { get; set; } = true;

    // ── Navigaatio-ominaisuudet ───────────────────────────────────────────────
    // Nämä eivät ole omia tietokantasarakkeitaan vaan kokoelmia,
    // jotka EF Core osaa täyttää viittausten kautta tarvittaessa.
    // Ne ladataan vain kun .Include(c => c.Tickets) lisätään kyselyyn (lazy → eager loading).

    /// <summary>Yrityksen käyttäjät portaalissa.</summary>
    // ICollection<ApplicationUser> = kokoelma käyttäjiä.
    // Yksi Customer → monta ApplicationUser -oliota (one-to-many).
    // "= new List<>()" estää NullReferenceException-poikkeuksen jos kokoelmaa
    // käytetään ennen kuin EF Core on ladannut sen.
    public ICollection<ApplicationUser>        Users                   { get; set; } = new List<ApplicationUser>();

    /// <summary>Yrityksen kaikki tiketit.</summary>
    // Yksi Customer → monta Ticket-oliota.
    public ICollection<Ticket>                 Tickets                 { get; set; } = new List<Ticket>();

    /// <summary>Yrityksen kaikki laskut.</summary>
    public ICollection<Invoice>                Invoices                { get; set; } = new List<Invoice>();

    /// <summary>Yrityksen palvelusopimukset.</summary>
    // Sopimus määrittää mitä palveluita asiakas saa ja kuukausimaksun.
    public ICollection<Contract>               Contracts               { get; set; } = new List<Contract>();

    /// <summary>Yrityksen etätyöpöytäyhteydet (RDP/VNC).</summary>
    // Tallennetut yhteydet, joita asiakas voi avata selaimen kautta Apache Guacamolen avulla.
    public ICollection<RemoteDesktopConnection> RemoteDesktopConnections { get; set; } = new List<RemoteDesktopConnection>();
}
