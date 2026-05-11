// Nimiavaruus — tiedosto kuuluu domain-mallien ryhmään.
namespace HakaTech.Portal.Models.Domain;

/// <summary>Laskun elinkaaren tila luonnoksesta maksettuun.</summary>
// Enum: nimetyt vakiot laskun eri vaiheille.
public enum InvoiceStatus
{
    Draft,    // Arvo 0 — Luonnos: admin on luonut laskun mutta ei vielä lähettänyt.
    Sent,     // Arvo 1 — Lähetetty: asiakas on saanut laskun, mutta ei ole vielä reagoinut.
    Unpaid,   // Arvo 2 — Maksamaton: eräpäivä ei ole vielä mennyt, mutta ei maksettu.
    Paid,     // Arvo 3 — Maksettu: suoritus vastaanotettu, lasku voidaan arkistoida.
    Overdue   // Arvo 4 — Erääntynyt: eräpäivä mennyt eikä maksettu — vaatii toimenpiteitä.
}

/// <summary>
/// Lasku, joka koostuu yhdestä tai useammasta laskurivistä (<see cref="InvoiceLine"/>).
/// Kokonaissummat ja ALV lasketaan dynaamisesti riveistä.
/// </summary>
// Lasku ei tallenna kokonaissummaa suoraan tietokantaan, vaan se
// LASKETAAN aina riveistä — näin luvut ovat aina synkronissa.
public class Invoice
{
    // Pääavain — EF Core kasvattaa automaattisesti jokaista uutta laskua kohti.
    public int Id { get; set; }

    /// <summary>Uniikki laskunumero, esim. "INV-2026-001".</summary>
    // Muoto generoidaan automaattisesti InvoiceController:ssa (SuggestInvoiceNumber-metodi).
    // Tietokannassa UNIQUE-rajoite — sama laskunumero ei voi esiintyä kahdessa laskussa.
    public string InvoiceNumber { get; set; } = string.Empty;

    // Laskun elinkaaren tila — oletusarvo Draft = uusi lasku on aina luonnos.
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>Laskun päiväys.</summary>
    // Milloin lasku on laadittu. Näytetään laskulla asiakkaalle.
    public DateTime InvoiceDate { get; set; }

    /// <summary>Eräpäivä — yleensä laskun päiväys + 14 päivää.</summary>
    // Eräpäivän lähestymistä seurataan dashboardin "Lähestyvät eräpäivät" -widgetissä.
    // Kun eräpäivä menee ohitse ilman maksua → lasku siirtyy Overdue-tilaan.
    public DateTime DueDate     { get; set; }

    /// <summary>Maksupäivä, null jos vielä maksamaton.</summary>
    // "DateTime?" = nullable — arvo asetetaan kun admin merkitsee laskun maksetuksi.
    public DateTime? PaidAt     { get; set; }

    /// <summary>Vapaa lisäteksti laskulle.</summary>
    // Esimerkiksi viitetieto tai erityisohje asiakkaalle. Ei pakollinen.
    public string? Notes { get; set; }

    // Viiteavain Customer-tauluun — lasku täytyy aina kuulua jollekin yritykselle.
    public int CustomerId { get; set; }
    // Navigaatio-ominaisuus: EF Core täyttää tämän .Include(i => i.Customer)-kutsulla.
    public Customer? Customer { get; set; }

    /// <summary>Laskun yksittäiset rivit.</summary>
    // Jokaisella rivillä on kuvaus, kappalemäärä ja yksikköhinta.
    // Tyhjä lista oletuksena — rivit lisätään luomisvaiheessa.
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

    /// <summary>Laskuun liitetyt tiedostot (esim. ulkopuolisten laskujen kopiot).</summary>
    // Liitteitä voivat lisätä vain admin-käyttäjät.
    public ICollection<InvoiceAttachment> Attachments { get; set; } = new List<InvoiceAttachment>();

    // ── Lasketut summat ────────────────────────────────────────────────────────
    // Huom: nämä ominaisuudet käyttävät "=>" eli ne ovat laskettuja (computed properties).
    // Niillä ei ole "set"-osaa → ne eivät tallennu tietokantaan,
    // vaan lasketaan aina uudelleen kun niitä luetaan.
    // Tämä takaa että summat ovat aina oikein, eikä koskaan vanhentuneita.

    /// <summary>Verollinen välisumma (rivien summa ennen ALV:tä).</summary>
    // "=>" = lambda-lauseke: laskee summan reaaliaikaisesti Lines-kokoelmasta.
    // LINQ:n Sum()-metodi käy läpi kaikki rivit ja laskee yhteen Quantity × UnitPrice.
    public decimal SubTotal    => Lines.Sum(l => l.Quantity * l.UnitPrice);

    /// <summary>ALV-summa (välisumma × ALV-prosentti).</summary>
    // Tässä käytetään toista laskettua ominaisuutta (SubTotal) laskennassa.
    // Esim: SubTotal = 1000 €, VatRate = 0.255 → VatAmount = 255 €
    public decimal VatAmount   => SubTotal * VatRate;

    /// <summary>Loppusumma asiakkaalle (välisumma + ALV).</summary>
    // Tämä on se summa, jonka asiakas maksaa.
    // Esim: SubTotal = 1000 € + VatAmount = 255 € → TotalAmount = 1255 €
    public decimal TotalAmount => SubTotal + VatAmount;

    /// <summary>ALV-prosentti desimaalina, esim. 0.255 = 25,5 % (Suomen yleinen ALV).</summary>
    // "decimal" = tarkka desimaalimuoto, sopii rahasummille (float/double pyöristysvirheitä).
    // Oletusarvo 0.255m = 25,5 % ALV (m-pääte tarkoittaa decimal-vakiota C#:ssa).
    // Voidaan muuttaa per lasku jos käytetään eri ALV-prosenttia (esim. 0 % vientilaskuissa).
    public decimal VatRate     { get; set; } = 0.255m;
}
