// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>Varattavan aikaikkunan tyyppi.</summary>
// Tyyppi vaikuttaa siihen miten slotti näytetään kalenterissa.
public enum BookingSlotType
{
    Maintenance,   // Huolto: palvelin- tai laitehuollot — tekninen käynti.
    Consulting,    // Konsultointi: asiantuntijatapaaminen — neuvottelua tai suunnittelua.
    RemoteSupport  // Etätuki: etäyhteyssessio — admin ottaa etäyhteyden asiakkaan koneelle.
}

/// <summary>
/// Aikaikkuna, jonka admin avaa varattavaksi. Asiakkaat voivat tehdä
/// siihen yksittäisiä varauksia (<see cref="Booking"/>). Slotti voi sallia
/// useita varauksia jos MaxCapacity > 1 (esim. ryhmäkoulutus).
/// </summary>
// BookingSlot on kuin auditoriossa oleva istumapaikka tai koulutustilaisuuden paikka:
// se on olemassa riippumatta siitä onko joku varannut sen.
// Yksi slotti → monta Booking-oliota (one-to-many).
public class BookingSlot
{
    // Pääavain.
    public int    Id              { get; set; }

    /// <summary>Slotin otsikko, näkyy kalenterissa.</summary>
    // Esim. "Palvelinhuolto", "IT-konsultointi", "Microsoft 365 -koulutus".
    public string Title           { get; set; } = string.Empty;

    /// <summary>Vapaa tarkennus mitä slotti sisältää.</summary>
    // Nullable — lyhyt kuvaus valinnainen.
    public string? Description    { get; set; }

    // Slotin palvelutyyppi — näkyy kalenterissa eri värinä tai ikonina.
    public BookingSlotType SlotType { get; set; }

    /// <summary>
    /// Slotin alkuaika. Tallennetaan paikallisena aikana (ei UTC),
    /// koska kalenterivaraukset ovat aina paikallisaikaa.
    /// </summary>
    // Poikkeus UTC-sääntöön: kalenterivaraukset ovat aina paikallisaikaa.
    // Jos tallennettaisiin UTC:nä, varaus näkyisi väärässä kohtaa kalenterissa
    // kun käyttäjä on eri aikavyöhykkeellä.
    public DateTime StartTime     { get; set; }

    /// <summary>Kesto minuuteissa.</summary>
    // int = kokonaisluku. Oletusarvo 60 = yksi tunti.
    // Käytetään laskemaan EndTime (ks. alla).
    public int DurationMinutes    { get; set; } = 60;

    /// <summary>Kuinka monta varausta slottiin mahtuu.</summary>
    // Oletusarvo 1 = yksilökohtainen tapaaminen (vain yksi asiakas).
    // MaxCapacity > 1 = ryhmätilaisuus (esim. 10 henkilöä samaan koulutukseen).
    public int MaxCapacity        { get; set; } = 1;

    /// <summary>Onko slotti varattavissa. False = piilotettu kalenterista.</summary>
    // Adminin kytkintä: false = slotti näkyy adminille mutta ei asiakkaille.
    public bool IsActive          { get; set; } = true;

    // Kuka slotin loi. Nullable koska se voi olla luotu ennen kuin käyttäjäjärjestelmä oli käytössä.
    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }

    /// <summary>Slottiin tehdyt varaukset (sisältää myös peruutetut).</summary>
    // Tyhjä lista oletuksena. "[]" = uusi C# 12 -syntaksi tyhjälle listalle (sama kuin new List<>()).
    public ICollection<Booking> Bookings { get; set; } = [];

    // ── Lasketut ominaisuudet ────────────────────────────────────────────────
    // Nämä eivät tallennu kantaan — ne lasketaan reaaliaikaisesti muista kentistä.

    /// <summary>Slotin päättymisaika (alkuaika + kesto).</summary>
    // "=>" = laskettu ominaisuus. AddMinutes lisää keston alkuaikaan.
    // Esim. StartTime = 9:00, DurationMinutes = 60 → EndTime = 10:00.
    public DateTime EndTime => StartTime.AddMinutes(DurationMinutes);

    /// <summary>Voimassa olevien (ei-peruutettujen) varausten lukumäärä.</summary>
    // Count() LINQ-metodi laskee ne varaukset joiden tila ei ole Cancelled.
    // Tämän avulla tiedetään kuinka moni paikka on täynnä.
    public int ActiveBookingsCount =>
        Bookings.Count(b => b.Status != BookingStatus.Cancelled);

    /// <summary>Vapaiden paikkojen määrä.</summary>
    // Maksimi miinus käytetyt = vapaat. Esim. MaxCapacity=10, ActiveBookings=3 → Available=7.
    public int AvailableSpots => MaxCapacity - ActiveBookingsCount;

    /// <summary>Onko slotti täynnä.</summary>
    // "<= 0" eikä "== 0" varmuuden vuoksi — teoriassa ei voi mennä alle nollan,
    // mutta defensiivinen ohjelmointi on hyvä tapa.
    public bool IsFull => AvailableSpots <= 0;

    /// <summary>Onko slotti jo mennyt.</summary>
    // DateTime.Now = paikallinen aika (ei UTC, koska StartTime on myös paikallisaika).
    public bool IsPast => StartTime < DateTime.Now;

    /// <summary>Voiko slottiin tehdä varauksen juuri nyt.</summary>
    // Kaikki kolme ehtoa täytyy olla totta: aktiivinen, ei mennyt ja ei täynnä.
    // "&&" = ja-operaattori: kaikki ehdot vaaditaan.
    public bool IsAvailable => IsActive && !IsPast && !IsFull;
}
