namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Audit-loki: kuka teki mitä, mille kohteelle ja milloin.
/// Käytetään tietoturvatutkintaan ja muutoshistorian tarkasteluun.
/// </summary>
public class AuditLog
{
    public int      Id          { get; set; }

    /// <summary>Tapahtuman aikaleima (UTC).</summary>
    public DateTime Timestamp   { get; set; } = DateTime.UtcNow;

    /// <summary>Tapahtuman aiheuttaneen käyttäjän ID. null jos järjestelmä tai anonyymi.</summary>
    public string? UserId    { get; set; }

    /// <summary>
    /// Käyttäjän sähköposti tapahtumahetkellä (denormalisoitu).
    /// Säilytetään tähän erikseen, jotta tieto säilyy vaikka käyttäjä poistettaisiin.
    /// </summary>
    public string? UserEmail { get; set; }

    /// <summary>Mitä tehtiin. Esim. Login, TicketCreated, TicketStatusChanged, InvoiceDownloaded, UserCreated.</summary>
    public string  Action     { get; set; } = string.Empty;

    /// <summary>Kohdetyyppi. Esim. Ticket, Invoice, User.</summary>
    public string? EntityType { get; set; }

    /// <summary>Kohteen tunniste (esim. tiketin numero).</summary>
    public string? EntityId   { get; set; }

    /// <summary>Vapaamuotoinen lisätieto, esim. vanha vs. uusi arvo.</summary>
    public string? Details    { get; set; }

    /// <summary>Pyynnön IP-osoite — auttaa epäilyttävän toiminnan jäljittämisessä.</summary>
    public string? IpAddress  { get; set; }
}
