using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HakaTech.Portal.Models.Domain;

namespace HakaTech.Portal.Models.ViewModels;

public class BookingCalendarViewModel
{
    public int  Year        { get; set; }
    public int  Month       { get; set; }
    public int? SelectedDay { get; set; }

    /// <summary>null = kaikki palvelutyypit</summary>
    public BookingSlotType? ServiceTypeFilter { get; set; }

    public List<BookingSlot> AllSlots        { get; set; } = [];
    public HashSet<int>      MyBookedSlotIds { get; set; } = [];
    public bool              IsAdmin         { get; set; }

    // ── Kalenteriapurit ─────────────────────────────────────────────
    public DateTime FirstDay     => new(Year, Month, 1);
    public int DaysInMonth       => DateTime.DaysInMonth(Year, Month);

    /// <summary>0 = Maanantai … 6 = Sunnuntai (suomalainen kalenteri)</summary>
    public int MonthStartOffset  => ((int)FirstDay.DayOfWeek + 6) % 7;

    public string MonthName      =>
        new DateTime(Year, Month, 1).ToString("MMMM yyyy", new CultureInfo("fi-FI"));

    public (int year, int month) PrevMonth =>
        Month == 1 ? (Year - 1, 12) : (Year, Month - 1);

    public (int year, int month) NextMonth =>
        Month == 12 ? (Year + 1, 1) : (Year, Month + 1);

    public IEnumerable<BookingSlot> SlotsOnDay(int day) =>
        AllSlots.Where(s => s.StartTime.Day == day);

    public List<BookingSlot> DisplayedSlots =>
        SelectedDay.HasValue
            ? AllSlots.Where(s => s.StartTime.Day == SelectedDay.Value).ToList()
            : AllSlots;
}

public class BookingSlotFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Otsikko on pakollinen.")]
    [StringLength(200)]
    [Display(Name = "Otsikko")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Kuvaus")]
    public string? Description { get; set; }

    [Display(Name = "Tyyppi")]
    public BookingSlotType SlotType { get; set; }

    [Required(ErrorMessage = "Alkamisaika on pakollinen.")]
    [Display(Name = "Alkamisaika")]
    public DateTime StartTime { get; set; } = DateTime.Now.Date.AddDays(1).AddHours(9);

    [Range(15, 480, ErrorMessage = "Keston on oltava 15–480 minuuttia.")]
    [Display(Name = "Kesto (min)")]
    public int DurationMinutes { get; set; } = 60;

    [Range(1, 50)]
    [Display(Name = "Maks. varauksia")]
    public int MaxCapacity { get; set; } = 1;

    [Display(Name = "Aktiivinen")]
    public bool IsActive { get; set; } = true;
}

public class BookingFormViewModel
{
    [Required]
    public int BookingSlotId { get; set; }

    [StringLength(1000)]
    [Display(Name = "Lisätiedot")]
    public string? Notes { get; set; }
}

public class BookingMyViewModel
{
    public List<Booking> Upcoming { get; set; } = [];
    public List<Booking> Past     { get; set; } = [];
}
