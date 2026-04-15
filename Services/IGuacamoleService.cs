using HakaTech.Portal.Models.Domain;

namespace HakaTech.Portal.Services;

public interface IGuacamoleService
{
    /// <summary>
    /// Kirjautuu Guacamoleen admin-tunnuksilla ja rakentaa yhteys-URL:n.
    /// Palauttaa null jos Guacamole ei ole konfiguroitu tai kirjautuminen epäonnistuu.
    /// </summary>
    Task<string?> BuildConnectionUrlAsync(RemoteDesktopConnection connection);

    /// <summary>Salaa salasanan IDataProtectorilla tallennusta varten.</summary>
    string ProtectPassword(string plaintext);
}
