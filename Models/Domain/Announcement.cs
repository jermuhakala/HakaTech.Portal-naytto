// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>Tiedotteen tyyppi — ohjaa myös käyttöliittymän väritystä.</summary>
// Enum: kolme erilaista tiedotetyyppiä, joilla on eri visuaalinen ilme.
public enum AnnouncementType
{
    Info,         // Arvo 0 — Yleinen tiedote: sininen värikoodi (neutraali tieto).
    Maintenance,  // Arvo 1 — Suunniteltu huoltokatko: oranssi värikoodi (varoitus mutta ennakkoilmoitus).
    Warning       // Arvo 2 — Varoitus tai akuutti häiriö: punainen värikoodi (toimii vaatii toimenpiteitä).
}

/// <summary>
/// Admin-ylläpitäjän tekemä tiedote, joka näkyy käyttäjille portaalin etusivulla.
/// Tiedotteella voi olla aikaikkuna (ValidFrom...ValidUntil), jonka aikana se on aktiivinen.
/// </summary>
// Tiedotteet ovat kuin "info-bannereita" portaalin yläosassa.
// Esimerkkikäyttötapauksia:
//  - "Palvelinhuolto pe 23.5. klo 22–02, katkoja odotettavissa."
//  - "Uusi toiminnallisuus: voit nyt varata etätukiaikoja kalenterista."
public class Announcement
{
    // Pääavain — automaattisesti kasvava.
    public int Id { get; set; }

    /// <summary>Tiedotteen otsikko.</summary>
    // Lyhyt, max n. 300 merkkiä (rajoitetaan ViewModelissa).
    // Näytetään lihavoituna tiedotekortissa.
    public string           Title      { get; set; } = string.Empty;

    /// <summary>Tiedotteen tekstisisältö (voi sisältää HTML:ää, sanitoidaan näytettäessä).</summary>
    // Pidempi selitysteksti tiedotteen alla. Voi sisältää HTML-muotoilua.
    // TÄRKEYS: HTML sanitoidaan Ganss.Xss-kirjastolla ennen näyttämistä,
    // jotta haitallinen JavaScript ei pääse XSS-hyökkäykseen.
    public string           Content    { get; set; } = string.Empty;

    // Tiedotteen visuaalinen tyyppi — ohjaa CSS-luokan valintaa näkymässä.
    public AnnouncementType Type       { get; set; } = AnnouncementType.Info;

    /// <summary>Mistä alkaen tiedote näkyy. null = heti.</summary>
    // "DateTime?" = nullable. null-arvo tarkoittaa "näkyy heti julkaisemisesta".
    // Tulevaisuuden tiedotteet voidaan luoda etukäteen → ne alkavat näkyä oikeaan aikaan.
    public DateTime? ValidFrom  { get; set; }

    /// <summary>Mihin asti tiedote näkyy. null = ei vanhene.</summary>
    // null = tiedote pysyy näkyvissä kunnes admin piilottaa sen tai poistaa.
    // Esim. huoltokatko-tiedote asetetaan vanhenemaan huollon päätyttyä.
    public DateTime? ValidUntil { get; set; }

    /// <summary>Onko tiedote julkaistu. False = vain admin näkee (luonnos).</summary>
    // Julkaisukytkimen avulla admin voi luoda tiedotteita luonnoksena ennen julkaisua.
    // Oletusarvo true = heti luomisen jälkeen julkinen.
    public bool      IsPublished { get; set; } = true;

    // Kuka tiedotteen loi — tärkeä vastuullisuuden kannalta.
    public string CreatedByUserId { get; set; } = string.Empty;
    // Navigaatio luojan tietoihin.
    public ApplicationUser? CreatedByUser { get; set; }

    // Milloin tiedote luotiin.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
