namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Asiakasyritys. Yksi asiakas voi sisältää useita käyttäjiä, tikettejä,
/// laskuja ja sopimuksia.
/// </summary>
public class Customer
{
    /// <summary>Tietokannan pääavain (juokseva numero).</summary>
    public int Id { get; set; }

    /// <summary>Yrityksen virallinen nimi.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Y-tunnus — yksilöllinen tunniste suomalaisille yrityksille.</summary>
    public string BusinessId   { get; set; } = string.Empty;

    /// <summary>Yrityksen yleinen yhteyssähköposti (lasku-/tukiviestintä).</summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>Puhelinnumero (vapaaehtoinen).</summary>
    public string? Phone       { get; set; }

    /// <summary>Käyntiosoite (vapaaehtoinen).</summary>
    public string? Address     { get; set; }

    /// <summary>Milloin asiakas on lisätty järjestelmään (UTC).</summary>
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    /// <summary>Onko asiakkuus aktiivinen. Inaktiivisia ei näytetä yleisissä listoissa.</summary>
    public bool IsActive       { get; set; } = true;

    // ── Navigaatio-ominaisuudet ───────────────────────────────────
    // Nämä eivät ole omia tietokantasarakkeitaan vaan kokoelmia,
    // jotka EF Core osaa täyttää viittausten kautta tarvittaessa.

    /// <summary>Yrityksen käyttäjät portaalissa.</summary>
    public ICollection<ApplicationUser>        Users                   { get; set; } = new List<ApplicationUser>();

    /// <summary>Yrityksen kaikki tiketit.</summary>
    public ICollection<Ticket>                 Tickets                 { get; set; } = new List<Ticket>();

    /// <summary>Yrityksen kaikki laskut.</summary>
    public ICollection<Invoice>                Invoices                { get; set; } = new List<Invoice>();

    /// <summary>Yrityksen palvelusopimukset.</summary>
    public ICollection<Contract>               Contracts               { get; set; } = new List<Contract>();

    /// <summary>Yrityksen etätyöpöytäyhteydet (RDP/VNC).</summary>
    public ICollection<RemoteDesktopConnection> RemoteDesktopConnections { get; set; } = new List<RemoteDesktopConnection>();
}
