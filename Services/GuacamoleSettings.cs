namespace HakaTech.Portal.Services;

/// <summary>
/// Apache Guacamolen asetukset. Guacamole on selainpohjainen
/// etätyöpöytä-gateway, joka mahdollistaa RDP/VNC/SSH-yhteyksien
/// avaamisen suoraan selaimessa ilman erillistä asiakasohjelmaa.
///
/// Asetukset ladataan appsettings.json:in "GuacamoleSettings"-osasta.
/// </summary>
public class GuacamoleSettings
{
    /// <summary>Guacamole-palvelimen perus-URL, esim. "https://guac.example.fi".</summary>
    public string? BaseUrl        { get; set; }

    /// <summary>Adminin käyttäjätunnus Guacamoleen (yhteyksien hallintaa varten).</summary>
    public string? AdminUsername  { get; set; }

    /// <summary>Adminin salasana Guacamoleen.</summary>
    public string? AdminPassword  { get; set; }

    /// <summary>
    /// Guacamolen tietolähteen tunniste. Yleisin on "mysql", mutta esim.
    /// "postgresql" tai "ldap" ovat myös mahdollisia.
    /// </summary>
    public string  DataSource     { get; set; } = "mysql";

    /// <summary>True jos kaikki tarvittavat asetukset on määritelty.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(AdminUsername) &&
        !string.IsNullOrWhiteSpace(AdminPassword);
}
