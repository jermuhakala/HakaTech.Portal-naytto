namespace HakaTech.Portal.Models.Domain;

/// <summary>Varattavan aikaikkunan tyyppi.</summary>
public enum BookingSlotType
{
    Maintenance,   // Huolto (palvelin- tai laitehuollot)
    Consulting,    // Konsultointi (asiantuntijatapaaminen)
    RemoteSupport  // Etätuki (etäyhteyssessio)
}

/// <summary>
/// Aikaikkuna, jonka admin avaa varattavaksi. Asiakkaat voivat tehdä
/// siihen yksittäisiä varauksia (<see cref="Booking"/>). Slotti voi sallia
/// useita varauksia jos MaxCapacity > 1 (esim. ryhmäkoulutus).
/// </summary>
public class BookingSlot
{
    public int    Id              { get; set; }

    /// <summary>Slotin otsikko, näkyy kalenterissa.</summary>
    public string Title           { get; set; } = string.Empty;

    /// <summary>Vapaa tarkennus mitä slotti sisältää.</summary>
    public string? Description    { get; set; }

    public BookingSlotType SlotType { get; set; }

    /// <summary>
    /// Slotin alkuaika. Tallennetaan paikallisena aikana (ei UTC),
    /// koska kalenterivaraukset ovat aina paikallisaikaa.
    /// </summary>
    public DateTime StartTime     { get; set; }

    /// <summary>Kesto minuuteissa.</summary>
    public int DurationMinutes    { get; set; } = 60;

    /// <summary>Kuinka monta varausta slottiin mahtuu.</summary>
    public int MaxCapacity        { get; set; } = 1;

    /// <summary>Onko slotti varattavissa. False = piilotettu kalenterista.</summary>
    public bool IsActive          { get; set; } = true;

    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }

    /// <summary>Slottiin tehdyt varaukset (sisältää myös peruutetut).</summary>
    public ICollection<Booking> Bookings { get; set; } = [];

    // ── Lasketut ominaisuudet ────────────────────────────────────
    // Nämä eivät tallennu kantaan vaan johdetaan muista kentistä.

    /// <summary>Slotin päättymisaika (alkuaika + kesto).</summary>
    public DateTime EndTime => StartTime.AddMinutes(DurationMinutes);

    /// <summary>Voimassa olevien (ei-peruutettujen) varausten lukumäärä.</summary>
    public int ActiveBookingsCount =>
        Bookings.Count(b => b.Status != BookingStatus.Cancelled);

    /// <summary>Vapaiden paikkojen määrä.</summary>
    public int AvailableSpots => MaxCapacity - ActiveBookingsCount;

    /// <summary>Onko slotti täynnä.</summary>
    public bool IsFull => AvailableSpots <= 0;

    /// <summary>Onko slotti jo mennyt.</summary>
    public bool IsPast => StartTime < DateTime.Now;

    /// <summary>Voiko slottiin tehdä varauksen juuri nyt.</summary>
    public bool IsAvailable => IsActive && !IsPast && !IsFull;
}
