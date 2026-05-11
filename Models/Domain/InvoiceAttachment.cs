// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Laskuun liitetty tiedosto. Käytännössä esim. PDF-versio
/// alkuperäisestä laskusta tai ulkopuolisten kuluraporttien skannaukset.
/// </summary>
// Sama "metadata tietokannassa, tiedosto levyllä" -periaate kuin TicketAttachment:ssa.
// Adminin voi liittää esim. alihankkijan kuitteja laskun tueksi.
public class InvoiceAttachment
{
    // Pääavain.
    public int Id { get; set; }

    /// <summary>Alkuperäinen tiedostonimi (näkyy käyttäjälle).</summary>
    // Näkyy latauslinkissä — käyttäjä näkee tiedoston alkuperäisen nimen.
    public string FileName { get; set; } = string.Empty;

    /// <summary>Tiedoston suhteellinen polku levyllä.</summary>
    // Suhteellinen polku uploads-hakemistosta, esim. "invoices/456/kuitti.pdf".
    // Absoluuttinen polku lasketaan IFileStorageService.ResolveSafePath()-metodilla.
    public string FilePath { get; set; } = string.Empty;

    // Milloin tiedosto ladattiin.
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // ── Lataaja ────────────────────────────────────────────────────────────────
    // Vain adminit voivat lisätä liitteitä laskuille — siksi tämä on aina admin.
    public string UploadedByUserId { get; set; } = string.Empty;
    public ApplicationUser? UploadedByUser { get; set; }

    // ── Lasku, johon liite kuuluu ──────────────────────────────────────────────
    // Viiteavain Invoice-tauluun.
    public int InvoiceId { get; set; }
    // Navigaatio laskuun.
    public Invoice? Invoice { get; set; }
}
