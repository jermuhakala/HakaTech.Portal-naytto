namespace HakaTech.Portal.Services;

public class GuacamoleSettings
{
    public string? BaseUrl        { get; set; }
    public string? AdminUsername  { get; set; }
    public string? AdminPassword  { get; set; }
    public string  DataSource     { get; set; } = "mysql";

    /// <summary>Palauttaa true jos Guacamole on konfiguroitu.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(AdminUsername) &&
        !string.IsNullOrWhiteSpace(AdminPassword);
}
