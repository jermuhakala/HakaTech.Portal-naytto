namespace HakaTech.Portal.Models.Domain;

public enum BookingStatus
{
    Pending,    // Odottaa vahvistusta
    Confirmed,  // Vahvistettu
    Cancelled   // Peruutettu
}

public class Booking
{
    public int Id { get; set; }

    public int        BookingSlotId { get; set; }
    public BookingSlot? BookingSlot { get; set; }

    public int        CustomerId { get; set; }
    public Customer?  Customer   { get; set; }

    public string          UserId { get; set; } = string.Empty;
    public ApplicationUser? User  { get; set; }

    public string? Notes              { get; set; }
    public BookingStatus Status       { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt         { get; set; } = DateTime.UtcNow;

    public DateTime? CancelledAt      { get; set; }
    public string?   CancellationReason { get; set; }
}
