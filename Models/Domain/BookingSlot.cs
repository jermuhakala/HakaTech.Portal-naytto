namespace HakaTech.Portal.Models.Domain;

public enum BookingSlotType
{
    Maintenance,   // Huolto
    Consulting,    // Konsultointi
    RemoteSupport  // Etätuki
}

public class BookingSlot
{
    public int    Id              { get; set; }
    public string Title           { get; set; } = string.Empty;
    public string? Description    { get; set; }
    public BookingSlotType SlotType { get; set; }

    /// <summary>Tallennetaan paikallisena aikana (ei UTC).</summary>
    public DateTime StartTime     { get; set; }
    public int DurationMinutes    { get; set; } = 60;
    public int MaxCapacity        { get; set; } = 1;
    public bool IsActive          { get; set; } = true;

    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }

    public ICollection<Booking> Bookings { get; set; } = [];

    // ── Lasketut ominaisuudet ────────────────────────────────────────
    public DateTime EndTime => StartTime.AddMinutes(DurationMinutes);

    public int ActiveBookingsCount =>
        Bookings.Count(b => b.Status != BookingStatus.Cancelled);

    public int AvailableSpots => MaxCapacity - ActiveBookingsCount;

    public bool IsFull => AvailableSpots <= 0;

    public bool IsPast => StartTime < DateTime.Now;

    public bool IsAvailable => IsActive && !IsPast && !IsFull;
}
