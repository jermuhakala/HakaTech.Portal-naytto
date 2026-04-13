using HakaTech.Portal.Models.Domain;

namespace HakaTech.Portal.Services;

public interface IGuacamoleService
{
    /// <summary>
    /// Rakentaa allekirjoitetun Guacamole-URL:n yhteydelle.
    /// Palauttaa null jos Guacamole ei ole konfiguroitu.
    /// </summary>
    string? BuildConnectionUrl(RemoteDesktopConnection connection, string userEmail);

    /// <summary>Salaa salasanan IDataProtectorilla tallennusta varten.</summary>
    string ProtectPassword(string plaintext);
}
