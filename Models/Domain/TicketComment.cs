// Nimiavaruus — tämä luokka kuuluu domain-mallien ryhmään.
namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Kommentti yksittäiseen tikettiin. Kommentointi muodostaa
/// keskustelun asiakkaan ja admin-tukihenkilön välille.
/// </summary>
// Jokainen tietueensä tässä taulussa on yksi viesti tikettikeskustelussa —
// kuten sähköpostiketjun yksittäinen viesti.
public class TicketComment
{
    // Pääavain — tietokannan automaattisesti kasvava tunnistenumero.
    public int Id { get; set; }

    /// <summary>Kommentin tekstisisältö.</summary>
    // Tämä on itse viesti — asiakkaan kuvaus tai adminin vastaus.
    // Oletusarvo string.Empty suojaa null-virheiltä.
    public string Content   { get; set; } = string.Empty;

    /// <summary>
    /// True = sisäinen muistiinpano, jonka vain admin näkee.
    /// Asiakkaalle nämä rivit eivät renderöidy.
    /// </summary>
    // Sisäiset muistiinpanot ovat kuin post-it-lappu tiketin takaosassa —
    // adminit kommunikoivat keskenään ilman että asiakas näkee.
    // bool = totuusarvo; oletusarvo false = julkinen kommentti.
    public bool   IsInternal{ get; set; } = false;

    // Milloin kommentti luotiin. UTC-aika vakiomenetelmän mukaisesti.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Tiketti, johon kommentti kuuluu ───────────────────────────────────────
    // TicketId = viiteavain Ticket-tauluun. Pakollinen — kommentti ei voi olla
    // ilman tikettiä (toisin kuin tiketti, joka voi olla ilman kommentteja).
    public int    TicketId { get; set; }

    // Navigaatio-ominaisuus: EF Core täyttää tämän .Include(c => c.Ticket)-kutsulla.
    // Nullable koska navigaatioita ei aina ladata — riippuu haun Include-kutsuista.
    public Ticket? Ticket  { get; set; }

    // ── Kommentin kirjoittaja ─────────────────────────────────────────────────
    // AuthorId = viiteavain käyttäjätauluun (string-muotoinen GUID ASP.NET Identitystä).
    public string AuthorId { get; set; } = string.Empty;

    // Navigaatio kirjoittajan tietoihin (nimi, sähköposti).
    // Ladataan tarvittaessa: .ThenInclude(c => c.Author) tiketin haun yhteydessä.
    public ApplicationUser? Author { get; set; }
}
