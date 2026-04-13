namespace HakaTech.Portal.Services;

public class GuacamoleSettings
{
    public string? BaseUrl            { get; set; }
    public string  JsonSecretKey      { get; set; } = string.Empty;
    public int     TokenExpiryMinutes { get; set; } = 60;

    /// <summary>Palauttaa true jos Guacamole on konfiguroitu.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(JsonSecretKey);
}
