// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>Varauksen tila.</summary>
// Varauksen elinkaari on kolmivaiheinen: luotu → vahvistettu tai peruutettu.
public enum BookingStatus
{
    Pending,    // Arvo 0 — Odottaa: asiakkaan varaus vastaanotettu, admin ei vielä vahvistanut.
    Confirmed,  // Arvo 1 — Vahvistettu: admin hyväksyi, varaus on voimassa.
    Cancelled   // Arvo 2 — Peruutettu: joko asiakas tai admin perui varauksen.
}

/// <summary>
/// Asiakkaan varaus tiettyyn aikaikkunaan (<see cref="BookingSlot"/>).
/// Yksi slotti voi sisältää useamman varauksen jos MaxCapacity > 1.
/// </summary>
// Booking on "liitostaulun olio" — se yhdistää asiakkaan (Customer/User)
// ja aikaikkunan (BookingSlot) toisiinsa.
// Analogia: hotellivaraus = asiakas + huone + aika.
public class Booking
{
    // Pääavain.
    public int Id { get; set; }

    // ── Aikaikkuna, johon varaus tehdään ──────────────────────────────────────
    // BookingSlotId = viiteavain BookingSlot-tauluun.
    public int        BookingSlotId { get; set; }
    // Navigaatio: EF Core täyttää tämän kun ladataan .Include(b => b.BookingSlot).
    public BookingSlot? BookingSlot { get; set; }

    // ── Asiakas ja varauksen tehnyt käyttäjä ──────────────────────────────────
    // Molemmat tallennetaan: sekä yritys (CustomerId) että yksittäinen henkilö (UserId).
    // Näin voidaan suodattaa varaukset sekä yrityskohtaisesti että käyttäjäkohtaisesti.

    // Viiteavain Customer-tauluun.
    public int        CustomerId { get; set; }
    // Navigaatio asiakasyritykseen.
    public Customer?  Customer   { get; set; }

    // Viiteavain käyttäjään — kuka tarkalleen teki varauksen.
    public string          UserId { get; set; } = string.Empty;
    // Navigaatio käyttäjään (nimi, sähköposti jne.).
    public ApplicationUser? User  { get; set; }

    /// <summary>Vapaa lisätieto, esim. mitä halutaan käsitellä konsultaatiossa.</summary>
    // Asiakas voi kirjoittaa viestin varaukseen — esim. "Haluamme keskustella M365-lisensseistä."
    // Nullable — ei pakollinen.
    public string? Notes              { get; set; }

    // Varauksen tila elinkaaren mukaan. Pending oletuksena.
    public BookingStatus Status       { get; set; } = BookingStatus.Pending;
    // Milloin varaus tehtiin.
    public DateTime CreatedAt         { get; set; } = DateTime.UtcNow;

    /// <summary>Peruutuksen aikaleima. null jos varaus voimassa.</summary>
    // null = varaus ei ole peruutettu. Arvo asetetaan Cancel-toiminnossa.
    public DateTime? CancelledAt      { get; set; }

    /// <summary>Peruutuksen syy.</summary>
    // Valinnainen selitys: "Asiakkaalla oli este" tai "Aikataulumuutos".
    public string?   CancellationReason { get; set; }
}
