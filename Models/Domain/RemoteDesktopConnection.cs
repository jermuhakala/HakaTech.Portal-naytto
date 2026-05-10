namespace HakaTech.Portal.Models.Domain;

/// <summary>Etäyhteysprotokolla.</summary>
public enum RemoteDesktopProtocol
{
    Rdp,  // Remote Desktop Protocol — Windows-työpöytä
    Vnc,  // Virtual Network Computing — yleinen ristialustaprotokolla
    Ssh   // Secure Shell — komentorivi (lähinnä Linux/Unix)
}

/// <summary>
/// Tallennettu etätyöpöytäyhteys asiakkaan koneeseen. Yhteyttä käytetään
/// selaimen kautta Apache Guacamolen avulla, joten asiakas ei tarvitse
/// erillistä RDP-asiakasohjelmaa.
/// </summary>
public class RemoteDesktopConnection
{
    public int Id { get; set; }

    /// <summary>Yhteyden näyttönimi käyttäjälle.</summary>
    public string Name     { get; set; } = string.Empty;

    public RemoteDesktopProtocol Protocol { get; set; } = RemoteDesktopProtocol.Rdp;

    /// <summary>Kohdekoneen IP-osoite tai DNS-nimi.</summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>TCP-portti — RDP=3389, VNC=5900, SSH=22.</summary>
    public int    Port     { get; set; } = 3389;

    public string? Username          { get; set; }

    /// <summary>
    /// Salasana salattuna ASP.NET Core Data Protection -API:lla.
    /// Älä kirjoita selkokielisenä lokitiedostoihin tai näytöllä.
    /// </summary>
    public string? EncryptedPassword { get; set; }

    // ── RDP-spesifit asetukset ────────────────────────────────────

    /// <summary>Hyväksyykö palvelimen sertifikaatin sokeasti (itseallekirjoitettu).</summary>
    public bool   IgnoreCert { get; set; } = true;

    /// <summary>RDP-tietoturvataso: "any", "rdp", "tls", "nla".</summary>
    public string Security   { get; set; } = "any";

    /// <summary>Guacamole-palvelimen yhteyden ID (Guacamole hallinnoi yhteyksiä omalla puolellaan).</summary>
    public string? GuacamoleConnectionId { get; set; }

    /// <summary>Vapaa muistiinpano yhteydestä.</summary>
    public string?  Notes     { get; set; }

    /// <summary>Käytössä-tila — false piilottaa yhteyden listalta ilman poistoa.</summary>
    public bool     IsActive  { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int       CustomerId { get; set; }
    public Customer? Customer   { get; set; }
}
