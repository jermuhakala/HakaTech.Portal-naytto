// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Audit-loki: kuka teki mitä, mille kohteelle ja milloin.
/// Käytetään tietoturvatutkintaan ja muutoshistorian tarkasteluun.
/// </summary>
// Audit-loki on tärkeä tietoturvatyökalu:
//  - Jos jotain menee pieleen, voidaan jäljittää kuka teki mitä ja milloin.
//  - Vaaditaan usein tietosuojalainsäädännössä (esim. GDPR-auditoinnit).
//  - Kirjataan mm. kirjautumiset, tiketin tilan muutokset, laskujen lataukset.
//
// HUOM: Audit-lokia ei saa muokata jälkikäteen — se on vain kirjoitukseen tarkoitettu taulukko.
public class AuditLog
{
    // Pääavain — automaattisesti kasvava.
    public int      Id          { get; set; }

    /// <summary>Tapahtuman aikaleima (UTC).</summary>
    // Täsmällinen aika on kriittinen forensiikalle: tapahtumien järjestys täytyy pystyä
    // rekonstruoimaan. UTC varmistaa ettei aikavyöhyke aiheuta epäselvyyksiä.
    public DateTime Timestamp   { get; set; } = DateTime.UtcNow;

    /// <summary>Tapahtuman aiheuttaneen käyttäjän ID. null jos järjestelmä tai anonyymi.</summary>
    // "string?" = nullable. null-arvo tarkoittaa että tapahtuma oli automaattinen
    // (esim. järjestelmän ajastettu tehtävä) tai kirjautumaton käyttäjä yritti toimintoa.
    public string? UserId    { get; set; }

    /// <summary>
    /// Käyttäjän sähköposti tapahtumahetkellä (denormalisoitu).
    /// Säilytetään tähän erikseen, jotta tieto säilyy vaikka käyttäjä poistettaisiin.
    /// </summary>
    // "Denormalisoitu" = sama tieto löytyy sekä Users-taulusta että tästä lokista.
    // Yleensä tietokantasuunnittelussa duplikaattitieto on huono asia, mutta lokissa
    // se on tietoinen päätös: jos käyttäjä poistetaan, lokimerkintä säilyy silti luettavana.
    public string? UserEmail { get; set; }

    /// <summary>Mitä tehtiin. Esim. Login, TicketCreated, TicketStatusChanged, InvoiceDownloaded, UserCreated.</summary>
    // Tapahtuman lyhyt koodi — vakiomääritelty, jotta voidaan suodattaa haussa.
    // Tähän käytetään PascalCase-muotoa: "TicketCreated", "LoginFailed" jne.
    public string  Action     { get; set; } = string.Empty;

    /// <summary>Kohdetyyppi. Esim. Ticket, Invoice, User.</summary>
    // Minkä tietotyypin kohteeseen toiminto kohdistui.
    // Yhdessä EntityId:n kanssa muodostaa täsmällisen viitteen muutettuun tietueeseen.
    public string? EntityType { get; set; }

    /// <summary>Kohteen tunniste (esim. tiketin numero).</summary>
    // Esim. EntityType="Ticket", EntityId="42" tarkoittaa tiketti #42.
    // String-tyyppi vaikka yleensä numero — joissakin tapauksissa tunniste voi olla GUID.
    public string? EntityId   { get; set; }

    /// <summary>Vapaamuotoinen lisätieto, esim. vanha vs. uusi arvo.</summary>
    // Tallentaa muutoksen yksityiskohdat, esim. "Open → InProgress" tai "Rating: 4".
    // Maksimipituus käytännössä rajattu MVC-validoinnilla, mutta tietokannassa ei rajoitusta.
    public string? Details    { get; set; }

    /// <summary>Pyynnön IP-osoite — auttaa epäilyttävän toiminnan jäljittämisessä.</summary>
    // Jos samasta IP-osoitteesta tulee kymmeniä epäonnistuneita kirjautumisyrityksiä,
    // se voi viitata hyökkäysyritykseen (brute force).
    // AuditService tallentaa tämän automaattisesti HTTP-pyynnön yhteydessä.
    public string? IpAddress  { get; set; }
}
