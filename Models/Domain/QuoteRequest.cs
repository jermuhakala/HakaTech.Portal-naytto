// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>Tarjouspyynnön elinkaari.</summary>
// Viisivaiheinen elinkaari: saapunut → käsittelyssä → lähetetty → hyväksytty/hylätty.
public enum QuoteRequestStatus
{
    Pending,    // Arvo 0 — Saapunut: asiakas on lähettänyt tarjouspyynnön, admin ei ole reagoinut.
    InProgress, // Arvo 1 — Käsittelyssä: admin valmistelee tarjousta.
    Sent,       // Arvo 2 — Tarjous lähetetty asiakkaalle (sähköpostilla tai portaalin kautta).
    Accepted,   // Arvo 3 — Asiakas hyväksyi tarjouksen → tilaus etenee.
    Declined    // Arvo 4 — Asiakas hylkäsi tai admin perui tarjouksen.
}

/// <summary>
/// Asiakkaan tekemä tarjouspyyntö palvelukatalogin tuotteesta.
/// </summary>
// Kun asiakas näkee palvelukatalogin tuotteen (ServiceCatalogItem), hän voi
// pyytää tarjouksen. Admin saa ilmoituksen ja valmistaa tarjouksen.
// Toimii kuten sähköinen yhteydenottolomake palvelukatalogissa.
public class QuoteRequest
{
    // Pääavain.
    public int Id { get; set; }

    // ── Palvelu, josta tarjousta pyydetään ────────────────────────────────────
    // Viiteavain ServiceCatalogItem-tauluun.
    public int ServiceCatalogItemId { get; set; }
    // Navigaatio: minkä palvelun tarjouspyyntö tämä on.
    public ServiceCatalogItem? Service { get; set; }

    // ── Asiakas ja pyynnön tehnyt käyttäjä ───────────────────────────────────
    // Molemmat: yritys ja yksittäinen henkilö.
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    // Kuka käyttäjä lähetti pyynnön.
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    /// <summary>Asiakkaan vapaa viesti tarjouspyynnön mukaan.</summary>
    // Asiakas voi kertoa lisätietoja tarpeestaan.
    // Esim: "Tarvitsemme M365 Business Premium -lisenssit 15 käyttäjälle."
    public string? Message    { get; set; }

    /// <summary>Adminin sisäiset muistiinpanot — eivät näy asiakkaalle.</summary>
    // Admin kirjoittaa tähän esim. hintatiedot tai neuvottelun tilanteen.
    public string? AdminNotes { get; set; }

    // Tarjouspyynnön tila elinkaaren mukaan. Pending oletuksena.
    public QuoteRequestStatus Status    { get; set; } = QuoteRequestStatus.Pending;
    // Milloin pyyntö luotiin.
    public DateTime           CreatedAt { get; set; } = DateTime.UtcNow;
    // Milloin pyyntöä viimeksi päivitettiin (tilan muutos päivittää tämän).
    public DateTime           UpdatedAt { get; set; } = DateTime.UtcNow;
}
