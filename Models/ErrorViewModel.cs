namespace HakaTech.Portal.Models;

/// <summary>
/// Virhesivun (Error.cshtml) malli. Sisältää pyynnön tunnisteen,
/// jota voi käyttää virheen jäljitykseen lokeissa.
/// </summary>
public class ErrorViewModel
{
    /// <summary>Yksilöllinen pyyntötunnus, jonka avulla virhe löytyy palvelimen lokista.</summary>
    public string? RequestId { get; set; }

    /// <summary>Näytetäänkö RequestId käyttäjälle (vain jos se on saatavilla).</summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
