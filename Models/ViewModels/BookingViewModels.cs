// Validointiattribuutit.
using System.ComponentModel.DataAnnotations;
// CultureInfo suomenkielisiä kuukausinimiä varten.
using System.Globalization;
// Domain-mallit.
using HakaTech.Portal.Models.Domain;

// Nimiavaruus.
namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Kalenterinäkymän malli. Sisältää valitun kuukauden, näytettävät
/// aikaikkunat ja apufunktiot kalenteriruudukon piirtämiseen.
/// </summary>
// Tämä ViewModel on "data + logiikka" -yhdistelmä:
// se sisältää dataa (Year, Month, AllSlots) mutta myös laskettuja apuominaisuuksia
// (FirstDay, DaysInMonth) jotka tekevät Razor-näkymän yksinkertaisemmaksi.
public class BookingCalendarViewModel
{
    // Näytettävä vuosi. Esim. 2026.
    public int  Year        { get; set; }
    // Näytettävä kuukausi numerona. Esim. 5 = toukokuu.
    public int  Month       { get; set; }
    // Jos käyttäjä on valinnut tietyn päivän, se tallennetaan tähän.
    // null = päivää ei ole valittu, näytetään koko kuukausi.
    public int? SelectedDay { get; set; }

    /// <summary>null = kaikki palvelutyypit</summary>
    // Suodatin: näytetään vain valitun tyypin aikaikkunat.
    // null = ei suodatusta, näytetään kaikki.
    public BookingSlotType? ServiceTypeFilter { get; set; }

    // Kaikki kuukauden aikaikkunat — ladattu tietokannasta controllerissa.
    public List<BookingSlot> AllSlots        { get; set; } = [];
    // Slotin ID:t joihin kirjautunut käyttäjä on jo varannut paikan.
    // HashSet = tehokas joukko — O(1) hakuaika: myBookedSlotIds.Contains(slotId).
    public HashSet<int>      MyBookedSlotIds { get; set; } = [];
    // Onko kirjautunut käyttäjä admin vai asiakaskäyttäjä.
    public bool              IsAdmin         { get; set; }

    // ── Kalenteriapurit ───────────────────────────────────────────────────────
    // Nämä ovat laskettuja ominaisuuksia (ei tietokantakenttiä).
    // Razor-näkymä voi kutsua näitä kuten tavallisia ominaisuuksia.

    // Kuukauden ensimmäinen päivä DateTime-oliona.
    // Esim. Year=2026, Month=5 → 2026-05-01 00:00:00
    public DateTime FirstDay     => new(Year, Month, 1);

    // Montako päivää kuukaudessa on (28/29/30/31 — huomioi karkausvuodet).
    public int DaysInMonth       => DateTime.DaysInMonth(Year, Month);

    /// <summary>0 = Maanantai … 6 = Sunnuntai (suomalainen kalenteri)</summary>
    // Suomalaisessa kalenterissa viikko alkaa maanantaista (0).
    // C#:n DayOfWeek-enum alkaa sunnuntaista (0) — siksi tarvitaan muunnos:
    // +6 siirtää sunnuntain loppuun, %7 pitää arvon 0–6 välillä.
    // Tulos: Mon=0, Tue=1, Wed=2, Thu=3, Fri=4, Sat=5, Sun=6.
    public int MonthStartOffset  => ((int)FirstDay.DayOfWeek + 6) % 7;

    // Kuukauden nimi suomeksi, esim. "toukokuu 2026".
    // CultureInfo("fi-FI") = suomalainen kulttuuriasetus → suomenkieliset kuukausinimet.
    public string MonthName      =>
        new DateTime(Year, Month, 1).ToString("MMMM yyyy", new CultureInfo("fi-FI"));

    // Edellinen kuukausi (tupleina: vuosi + kuukausi).
    // Tuple = kahden arvon pari. Esim. (2026, 4) tai (2025, 12) jos ollaan tammikuussa.
    public (int year, int month) PrevMonth =>
        Month == 1 ? (Year - 1, 12) : (Year, Month - 1);

    // Seuraava kuukausi.
    public (int year, int month) NextMonth =>
        Month == 12 ? (Year + 1, 1) : (Year, Month + 1);

    // Palauttaa kaikki tietyn päivän aikaikkunat.
    // LINQ:n Where() suodattaa ne slotit joiden päivä täsmää.
    public IEnumerable<BookingSlot> SlotsOnDay(int day) =>
        AllSlots.Where(s => s.StartTime.Day == day);

    // Palauttaa näytettävät slotit: jos päivä on valittu → vain sen päivän slotit,
    // muuten kaikki kuukauden slotit.
    public List<BookingSlot> DisplayedSlots =>
        SelectedDay.HasValue
            ? AllSlots.Where(s => s.StartTime.Day == SelectedDay.Value).ToList()
            : AllSlots;
}

/// <summary>Adminin lomake aikaikkunan luomiseen ja muokkaamiseen.</summary>
// Dual-purpose: sekä Create (Id=0) että Edit (Id>0).
public class BookingSlotFormViewModel
{
    // 0 = uusi slotti, >0 = olemassaoleva (muokkaus).
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
    // Oletusarvo = huomenna klo 9:00 — järkevä oletusarvo uudelle slotille.
    public DateTime StartTime { get; set; } = DateTime.Now.Date.AddDays(1).AddHours(9);

    // [Range(15, 480)] = keston täytyy olla 15–480 minuuttia (15 min – 8 tuntia).
    [Range(15, 480, ErrorMessage = "Keston on oltava 15–480 minuuttia.")]
    [Display(Name = "Kesto (min)")]
    // Oletusarvo 60 minuuttia = 1 tunti.
    public int DurationMinutes { get; set; } = 60;

    // Maksimi 1–50 varausta per slotti.
    [Range(1, 50)]
    [Display(Name = "Maks. varauksia")]
    public int MaxCapacity { get; set; } = 1;

    [Display(Name = "Aktiivinen")]
    public bool IsActive { get; set; } = true;
}

/// <summary>Asiakkaan lomake aikaikkunan varaamiseen.</summary>
// Pieni lomake — asiakas näkee vain "Lisätiedot"-kentän.
// BookingSlotId tulee piilotettuna kentänä lomakkeesta (asiakas ei valitse sitä).
public class BookingFormViewModel
{
    // [Required] = pakollinen. Tulee URL:n parametrista, ei käyttäjän syöttönä.
    [Required]
    public int BookingSlotId { get; set; }

    // Vapaaehtoinen viesti, esim. "Haluamme puhua M365-lisenssien uusimisesta."
    [StringLength(1000)]
    [Display(Name = "Lisätiedot")]
    public string? Notes { get; set; }
}

/// <summary>"Omat varaukset" -näkymän malli. Jaetaan tuleviin ja menneisiin.</summary>
// Asiakkaan näkymä omista varauksistaan jaetaan kahteen listaan
// selkeyden vuoksi: tulevat (toimenpiteitä vaativat) vs. menneet (historia).
public class BookingMyViewModel
{
    /// <summary>Tulevat varaukset, jotka eivät ole vielä alkaneet.</summary>
    // Lista tulevista varauksia — asiakas voi peruuttaa näitä.
    public List<Booking> Upcoming { get; set; } = [];

    /// <summary>Menneet varaukset (historia).</summary>
    // Lista jo menneistä tai peruutetuista varauksista — vain lukukäyttöön.
    public List<Booking> Past     { get; set; } = [];
}
