// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>Sopimustyyppi — määrittelee palvelutason ja hinnoittelumallin.</summary>
public enum ContractType
{
    Support24_7,     // 24/7 Pro: kriittiset häiriöt hoidetaan myös yöllä ja viikonloppuna.
    SupportBusiness, // Tuki arkisin klo 8–17: normaali toimistoaikainen tuki.
    Managed,         // Managed IT: kaikenkattava ulkoistus — HakaTech hoitaa kaiken IT:n.
    OneTime          // Kertasuoritus: yksittäinen toimitus tai projekti, ei jatkuvaa suhdetta.
}

/// <summary>
/// Asiakkaan ja HakaTechin välinen palvelusopimus. Määrää mitä palveluita
/// asiakas saa, kuukausihinnan ja sopimuskauden alku- ja loppupäivän.
/// </summary>
// Sopimus on asiakassuhteen perusta: se määrittelee mitä asiakas on oikeutettu saamaan.
// Tikettikiintiö (TicketQuota) rajoittaa kuinka monta tukipyyntöä kuukaudessa kuuluu sopimukseen.
public class Contract
{
    // Pääavain.
    public int Id { get; set; }

    // Sopimustyyppi — ohjaa mitä SLA-tasoa sovelletaan.
    public ContractType Type        { get; set; }

    /// <summary>Vapaamuotoinen kuvaus sopimuksen sisällöstä.</summary>
    // Esimerkiksi "M365-lisenssit, sähköpostipalvelin ja viikoittainen varmuuskopiointi".
    public string       Description { get; set; } = string.Empty;

    // Sopimuskauden alku- ja loppupäivä.
    // EF Core tallentaa nämä tietokantaan DateTime-sarakkeina.
    public DateTime StartDate { get; set; }
    public DateTime EndDate   { get; set; }

    /// <summary>Kuinka monta tikettiä sopimus kattaa kuukaudessa.</summary>
    // Oletusarvo 30 = enintään 30 tukipyyntöä kuukaudessa sopimuksen piirissä.
    // Jos ylitetään, ylimääräiset laskutetaan erikseen.
    public int  TicketQuota    { get; set; } = 30;

    /// <summary>Kuluvan kuukauden käytetyt tiketit (kiintiön seuranta).</summary>
    // Nollataan kuukauden vaihteessa. Kasvaa jokaisen tiketin luomisen myötä.
    // int oletusarvo on automaattisesti 0, mutta se kirjoitetaan näkyville selkeyden vuoksi.
    public int  TicketsUsed    { get; set; } = 0;

    /// <summary>Kuukausimaksu €. 0 = kertapalvelu.</summary>
    // "decimal" = tarkka laskenta rahasummille (ei pyöristysvirheitä kuten float/double).
    public decimal MonthlyPrice { get; set; }

    /// <summary>Onko sopimus voimassa (false = irtisanottu / vanhentunut).</summary>
    // IsActive = false kun sopimus on päättynyt tai irtisanottu.
    // Inaktiivisia sopimuksia ei näytetä normaalissa listauksessa.
    public bool    IsActive     { get; set; } = true;

    // Mille asiakasyritykselle tämä sopimus kuuluu.
    public int CustomerId { get; set; }
    // Navigaatio asiakasyritykseen.
    public Customer? Customer { get; set; }
}
