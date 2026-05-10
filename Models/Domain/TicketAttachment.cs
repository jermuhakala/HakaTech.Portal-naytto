namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Tikettiin liitetty tiedosto (esim. kuvakaappaus virheilmoituksesta tai lokitiedosto).
/// Itse tiedosto tallennetaan levylle, ja tähän tauluun jää tieto sijainnista ja lataajasta.
/// </summary>
public class TicketAttachment
{
    public int Id { get; set; }

    /// <summary>Alkuperäinen tiedostonimi (näkyy käyttäjälle).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Tiedoston suhteellinen polku levyllä (esim. "tickets/123/abc.png").</summary>
    public string FilePath { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // ── Lataaja ───────────────────────────────────────────────────
    public string UploadedByUserId { get; set; } = string.Empty;
    public ApplicationUser? UploadedByUser { get; set; }

    // ── Tiketti, johon liite kuuluu ───────────────────────────────
    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }
}
