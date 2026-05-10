namespace HakaTech.Portal.Models.Domain;

/// <summary>Varauksen tila.</summary>
public enum BookingStatus
{
    Pending,    // Odottaa adminin vahvistusta
    Confirmed,  // Vahvistettu, voidaan suorittaa
    Cancelled   // Peruutettu joko asiakkaan tai adminin toimesta
}

/// <summary>
/// Asiakkaan varaus tiettyyn aikaikkunaan (<see cref="BookingSlot"/>).
/// Yksi slotti voi sisältää useamman varauksen jos MaxCapacity > 1.
/// </summary>
public class Booking
{
    public int Id { get; set; }

    // ── Aikaikkuna, johon varaus tehdään ──────────────────────────
    public int        BookingSlotId { get; set; }
    public BookingSlot? BookingSlot { get; set; }

    // ── Asiakas ja varauksen tehnyt käyttäjä ──────────────────────
    public int        CustomerId { get; set; }
    public Customer?  Customer   { get; set; }

    public string          UserId { get; set; } = string.Empty;
    public ApplicationUser? User  { get; set; }

    /// <summary>Vapaa lisätieto, esim. mitä halutaan käsitellä konsultaatiossa.</summary>
    public string? Notes              { get; set; }

    public BookingStatus Status       { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt         { get; set; } = DateTime.UtcNow;

    /// <summary>Peruutuksen aikaleima. null jos varaus voimassa.</summary>
    public DateTime? CancelledAt      { get; set; }

    /// <summary>Peruutuksen syy.</summary>
    public string?   CancellationReason { get; set; }
}
