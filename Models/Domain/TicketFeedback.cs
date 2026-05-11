// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Asiakkaan jättämä palaute kun tiketti on suljettu. Yksi tiketti
/// voi saada vain yhden palautteen (uniikki indeksi DB:ssä).
/// </summary>
// Tämä on ns. "one-to-one" suhde tiketin kanssa:
// yhtä tikettiä kohti on korkeintaan yksi palauteolio.
// Tietokannan UNIQUE-rajoite estää useamman palautteen tallentamisen samalle tiketille.
public class TicketFeedback
{
    // Pääavain — automaattisesti kasvava.
    public int Id { get; set; }

    // ── Tiketti, jota palaute koskee ──────────────────────────────────────────
    // Viiteavain Ticket-tauluun — palaute täytyy kohdistua johonkin tikettiin.
    public int     TicketId { get; set; }
    // Navigaatio tikettiin — ladataan tarvittaessa.
    public Ticket? Ticket   { get; set; }

    // ── Palautteen jättäjä ─────────────────────────────────────────────────────
    // Kuka palautteen jätti. Vain tiketin alkuperäinen luoja voi jättää palautteen.
    public string           UserId { get; set; } = string.Empty;
    public ApplicationUser? User   { get; set; }

    /// <summary>Arvosana asteikolla 1–5 (1 = huono, 5 = erinomainen).</summary>
    // Kokonaisluku välillä 1–5. Validoidaan controllerissa ennen tallennusta.
    // Tätä arvoa käytetään raporttinäkymässä keskiarvolaskennassa.
    public int     Rating      { get; set; }

    /// <summary>Vapaamuotoinen sanallinen palaute (vapaaehtoinen).</summary>
    // "string?" = nullable. Asiakas ei ole pakollinen kirjoittamaan tekstipalautetta —
    // pelkkä tähtiarvo riittää.
    public string? Comment     { get; set; }

    // Milloin palaute jätettiin. UTC-aika.
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
