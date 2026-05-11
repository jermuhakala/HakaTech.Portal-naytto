// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Yksittäinen laskurivi: tuote/palvelu, määrä ja yksikköhinta.
/// Rivin summa on Quantity × UnitPrice.
/// </summary>
// Yksi lasku (Invoice) sisältää yhden tai useamman rivin (InvoiceLine).
// Ajattele kuin kaupan kassakuitti: jokainen tuote on oma rivinsä.
// InvoiceLine on laskun "lapsi" (parent = Invoice, child = InvoiceLine).
public class InvoiceLine
{
    // Pääavain — automaattisesti kasvava tunnistenumero.
    public int Id { get; set; }

    /// <summary>Rivin selite asiakkaalle (esim. "IT-tuki kuukausimaksu").</summary>
    // Tämä teksti näkyy asiakkaalle laskulla.
    // Kuvaava teksti auttaa asiakasta ymmärtämään mistä lasku koostuu.
    public string  Description { get; set; } = string.Empty;

    /// <summary>Kappalemäärä tai tunnit. Salli kymmenykset (esim. 3,5 h).</summary>
    // "decimal" sallii desimaalit — voidaan laskuttaa esim. 1,5 tuntia.
    // Oletusarvo 1 = yksi kappale tai yksi tunti.
    public decimal Quantity    { get; set; } = 1;

    /// <summary>Verollinen yksikköhinta (€).</summary>
    // Hinta yhtä yksikköä kohti. Kokonaissumma = Quantity × UnitPrice.
    // Veroton summa lasketaan Invoice.SubTotal-ominaisuudessa.
    public decimal UnitPrice   { get; set; }

    // Viiteavain — mille laskulle tämä rivi kuuluu.
    public int     InvoiceId { get; set; }
    // Navigaatio laskuun. EF Core täyttää tarvittaessa.
    public Invoice? Invoice  { get; set; }
}
