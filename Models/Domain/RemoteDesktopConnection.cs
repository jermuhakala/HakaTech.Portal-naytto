namespace HakaTech.Portal.Models.Domain;

public enum RemoteDesktopProtocol { Rdp, Vnc, Ssh }

public class RemoteDesktopConnection
{
    public int Id { get; set; }

    public string Name     { get; set; } = string.Empty;
    public RemoteDesktopProtocol Protocol { get; set; } = RemoteDesktopProtocol.Rdp;
    public string Hostname { get; set; } = string.Empty;
    public int    Port     { get; set; } = 3389;

    public string? Username          { get; set; }
    public string? EncryptedPassword { get; set; }  // IDataProtector-salattu

    // RDP-spesifit kentät
    public bool   IgnoreCert { get; set; } = true;
    public string Security   { get; set; } = "any";

    /// <summary>Guacamole-palvelimen yhteyden ID (haettu Guacamolen hallinnasta).</summary>
    public string? GuacamoleConnectionId { get; set; }

    public string?  Notes     { get; set; }
    public bool     IsActive  { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int       CustomerId { get; set; }
    public Customer? Customer   { get; set; }
}
