// Nimiavaruus — tiedosto kuuluu domain-mallien ryhmään.
namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Tikettiin liitetty tiedosto (esim. kuvakaappaus virheilmoituksesta tai lokitiedosto).
/// Itse tiedosto tallennetaan levylle, ja tähän tauluun jää tieto sijainnista ja lataajasta.
/// </summary>
// Tämä noudattaa "metadata tietokannassa, data levyllä" -periaatetta.
// Tietokanta on tehokas indeksointiin ja hakuihin, mutta tiedostojen tallentaminen
// suoraan BLOB-kenttänä hidastaa ja kasvattaa tietokannan kokoa — siksi tiedostot
// tallennetaan palvelimen levylle (wwwroot/uploads/) ja tässä taulussa säilytetään
// vain polku sinne.
public class TicketAttachment
{
    // Pääavain — automaattisesti kasvava tunnistenumero.
    public int Id { get; set; }

    /// <summary>Alkuperäinen tiedostonimi (näkyy käyttäjälle).</summary>
    // Tämä on tiedoston alkuperäinen nimi sellaisena kuin käyttäjä sen latasi.
    // Esim. "virheruutu_23042026.png" — näytetään liitetiedostolinkissä.
    public string FileName { get; set; } = string.Empty;

    /// <summary>Tiedoston suhteellinen polku levyllä (esim. "tickets/123/abc.png").</summary>
    // Suhteellinen polku tallennetaan, ei absoluuttinen — näin sovellus toimii
    // myös eri palvelimilla ja hakemistopoluilla.
    // Liitteen lataushetkel generoidaan uniikki tiedostonimi (GUID) turvallisuussyistä,
    // jotta käyttäjä ei voi arvata muiden tiedostojen polkuja.
    public string FilePath { get; set; } = string.Empty;

    // Milloin tiedosto ladattiin. UTC-aika.
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // ── Lataaja ────────────────────────────────────────────────────────────────
    // Kuka latasi tiedoston — asiakas vai admin. Tärkeä tietoturvan kannalta.
    public string UploadedByUserId { get; set; } = string.Empty;
    // Navigaatio lataajan tietoihin — nimi ja sähköposti.
    public ApplicationUser? UploadedByUser { get; set; }

    // ── Tiketti, johon liite kuuluu ────────────────────────────────────────────
    // Viiteavain Ticket-tauluun — liite täytyy kuulua tikettiin.
    public int TicketId { get; set; }
    // Navigaatio tikettiä kohti.
    public Ticket? Ticket { get; set; }
}
