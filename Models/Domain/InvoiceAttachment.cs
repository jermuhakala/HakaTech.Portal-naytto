namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Laskuun liitetty tiedosto. Käytännössä esim. PDF-versio
/// alkuperäisestä laskusta tai ulkopuolisten kuluraporttien skannaukset.
/// </summary>
public class InvoiceAttachment
{
    public int Id { get; set; }

    /// <summary>Alkuperäinen tiedostonimi (näkyy käyttäjälle).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Tiedoston suhteellinen polku levyllä.</summary>
    public string FilePath { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // ── Lataaja ───────────────────────────────────────────────────
    public string UploadedByUserId { get; set; } = string.Empty;
    public ApplicationUser? UploadedByUser { get; set; }

    // ── Lasku, johon liite kuuluu ─────────────────────────────────
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
}
