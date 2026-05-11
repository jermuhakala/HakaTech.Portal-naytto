// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>Etäyhteysprotokolla.</summary>
// Kolme yleisintä etäyhteysprotokollaa eri käyttötarkoituksiin.
public enum RemoteDesktopProtocol
{
    Rdp,  // Remote Desktop Protocol — Microsoftin standardi Windows-etätyöpöydälle. Portti 3389.
    Vnc,  // Virtual Network Computing — yleinen ristialustaprotokolla. Portti 5900.
    Ssh   // Secure Shell — suojattu komentorivi-yhteys, lähinnä Linux/Unix-palvelimille. Portti 22.
}

/// <summary>
/// Tallennettu etätyöpöytäyhteys asiakkaan koneeseen. Yhteyttä käytetään
/// selaimen kautta Apache Guacamolen avulla, joten asiakas ei tarvitse
/// erillistä RDP-asiakasohjelmaa.
/// </summary>
// Apache Guacamole on avoimen lähdekoodin palvelin, joka toimii "välityspalvelimena":
// selain lähettää HTTP-pyynnön → Guacamole muodostaa RDP/VNC/SSH-yhteyden palvelimelle
// → lähettää kuvavirran selaimelle. Asiakas näkee etäkoneen kuin selainsivun.
//
// TIETOTURVA: Salasanat salataan ASP.NET Core Data Protection -API:lla ennen tallennusta.
// Selkokielinen salasana ei koskaan tallennu tietokantaan.
public class RemoteDesktopConnection
{
    // Pääavain.
    public int Id { get; set; }

    /// <summary>Yhteyden näyttönimi käyttäjälle.</summary>
    // Esim. "Toimiston palvelin", "HR-tietokone" tai "Webbi-palvelin 1".
    public string Name     { get; set; } = string.Empty;

    // Protokolla: RDP, VNC tai SSH.
    public RemoteDesktopProtocol Protocol { get; set; } = RemoteDesktopProtocol.Rdp;

    /// <summary>Kohdekoneen IP-osoite tai DNS-nimi.</summary>
    // Esim. "192.168.1.100" tai "server.hakatech.fi".
    // Tähän osoitteeseen Guacamole muodostaa yhteyden.
    public string Hostname { get; set; } = string.Empty;

    /// <summary>TCP-portti — RDP=3389, VNC=5900, SSH=22.</summary>
    // Oletusarvo 3389 = RDP:n oletusportti. Voidaan muuttaa jos kohde käyttää eri porttia.
    public int    Port     { get; set; } = 3389;

    // Kirjautumistunnus etäkoneelle. Nullable — ei aina tarvita.
    public string? Username          { get; set; }

    /// <summary>
    /// Salasana salattuna ASP.NET Core Data Protection -API:lla.
    /// Älä kirjoita selkokielisenä lokitiedostoihin tai näytöllä.
    /// </summary>
    // Data Protection -API salaa datan ja sitoo sen tähän tiettyyn sovellukseen ja palvelimeen.
    // Vaikka tietokanta vuotaisi, salatut salasanat eivät ole hyödynnettävissä ilman
    // sovelluksen salausavainta.
    // "Encrypted" nimessä kertoo kehittäjille: älä käsittele tätä kenttää suoraan.
    public string? EncryptedPassword { get; set; }

    // ── RDP-spesifit asetukset ─────────────────────────────────────────────────

    /// <summary>Hyväksyykö palvelimen sertifikaatin sokeasti (itseallekirjoitettu).</summary>
    // Tuotantoympäristöissä sertifikaatti pitäisi olla virallinen, mutta
    // sisäverkossa on yleistä käyttää itseallekirjoitettuja — siksi oletuksena true.
    public bool   IgnoreCert { get; set; } = true;

    /// <summary>RDP-tietoturvataso: "any", "rdp", "tls", "nla".</summary>
    // "any" = sovitaan yhteydenmuodostuksessa automaattisesti sopivasta tasosta.
    // "nla" = Network Level Authentication — turvallisempi mutta vaatii tuen molemmilta osapuolilta.
    public string Security   { get; set; } = "any";

    /// <summary>Guacamole-palvelimen yhteyden ID (Guacamole hallinnoi yhteyksiä omalla puolellaan).</summary>
    // Guacamole-palvelin numeroi omat yhteytensä — tämä kenttä tallentaa sen tunnuksen.
    // Null = yhteyttä ei ole vielä rekisteröity Guacamolelle.
    public string? GuacamoleConnectionId { get; set; }

    /// <summary>Vapaa muistiinpano yhteydestä.</summary>
    // Esim. "Tämä on asiakkaan X varmuuskopiointipalvelin. Ei huoltotoimia ilman ilmoitusta."
    public string?  Notes     { get; set; }

    /// <summary>Käytössä-tila — false piilottaa yhteyden listalta ilman poistoa.</summary>
    // Soft delete -periaate: yhteyttä ei poisteta kokonaan, vaan piilotetaan.
    // Historia ja asetukset säilyvät, ja yhteys voidaan aktivoida uudelleen.
    public bool     IsActive  { get; set; } = true;

    // Milloin yhteys luotiin.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Mille asiakasyritykselle yhteys kuuluu.
    public int       CustomerId { get; set; }
    public Customer? Customer   { get; set; }
}
