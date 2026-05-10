namespace HakaTech.Portal.Models.Domain;

/// <summary>Laskun elinkaaren tila luonnoksesta maksettuun.</summary>
public enum InvoiceStatus
{
    Draft,    // Luonnos — ei vielä lähetetty asiakkaalle
    Sent,     // Lähetetty asiakkaalle
    Unpaid,   // Lähetetty mutta maksamatta (eräpäivä ei vielä mennyt)
    Paid,     // Maksettu
    Overdue   // Erääntynyt — maksamatta ja eräpäivä mennyt
}

/// <summary>
/// Lasku, joka koostuu yhdestä tai useammasta laskurivistä (<see cref="InvoiceLine"/>).
/// Kokonaissummat ja ALV lasketaan dynaamisesti riveistä.
/// </summary>
public class Invoice
{
    public int Id { get; set; }

    /// <summary>Uniikki laskunumero, esim. "INV-2026-001".</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>Laskun päiväys.</summary>
    public DateTime InvoiceDate { get; set; }

    /// <summary>Eräpäivä — yleensä laskun päiväys + 14 päivää.</summary>
    public DateTime DueDate     { get; set; }

    /// <summary>Maksupäivä, null jos vielä maksamaton.</summary>
    public DateTime? PaidAt     { get; set; }

    /// <summary>Vapaa lisäteksti laskulle.</summary>
    public string? Notes { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Laskun yksittäiset rivit.</summary>
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

    /// <summary>Laskuun liitetyt tiedostot (esim. ulkopuolisten laskujen kopiot).</summary>
    public ICollection<InvoiceAttachment> Attachments { get; set; } = new List<InvoiceAttachment>();

    // ── Lasketut summat ───────────────────────────────────────────
    // Nämä eivät tallennu kantaan vaan lasketaan aina riveistä,
    // jotta luvut pysyvät synkronissa.

    /// <summary>Verollinen välisumma (rivien summa ennen ALV:tä).</summary>
    public decimal SubTotal    => Lines.Sum(l => l.Quantity * l.UnitPrice);

    /// <summary>ALV-summa (välisumma × ALV-prosentti).</summary>
    public decimal VatAmount   => SubTotal * VatRate;

    /// <summary>Loppusumma asiakkaalle (välisumma + ALV).</summary>
    public decimal TotalAmount => SubTotal + VatAmount;

    /// <summary>ALV-prosentti desimaalina, esim. 0.255 = 25,5 % (Suomen yleinen ALV).</summary>
    public decimal VatRate     { get; set; } = 0.255m;
}
