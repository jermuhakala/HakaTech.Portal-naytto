namespace HakaTech.Portal.Models.Domain;

/// <summary>Sopimustyyppi — määrittelee palvelutason ja hinnoittelumallin.</summary>
public enum ContractType
{
    Support24_7,     // 24/7 Pro -tuki: kriittiset häiriöt päivystysaikana
    SupportBusiness, // Tuki arkisin (klo 8–17)
    Managed,         // Managed IT — kaiken kattava ulkoistus
    OneTime          // Kertaluonteinen toimitus, ei jatkuva
}

/// <summary>
/// Asiakkaan ja HakaTechin välinen palvelusopimus. Määrää mitä palveluita
/// asiakas saa, kuukausihinnan ja sopimuskauden alku- ja loppupäivän.
/// </summary>
public class Contract
{
    public int Id { get; set; }

    public ContractType Type        { get; set; }

    /// <summary>Vapaamuotoinen kuvaus sopimuksen sisällöstä.</summary>
    public string       Description { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime EndDate   { get; set; }

    /// <summary>Kuinka monta tikettiä sopimus kattaa kuukaudessa.</summary>
    public int  TicketQuota    { get; set; } = 30;

    /// <summary>Kuluvan kuukauden käytetyt tiketit (kiintiön seuranta).</summary>
    public int  TicketsUsed    { get; set; } = 0;

    /// <summary>Kuukausimaksu €. 0 = kertapalvelu.</summary>
    public decimal MonthlyPrice { get; set; }

    /// <summary>Onko sopimus voimassa (false = irtisanottu / vanhentunut).</summary>
    public bool    IsActive     { get; set; } = true;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
}
