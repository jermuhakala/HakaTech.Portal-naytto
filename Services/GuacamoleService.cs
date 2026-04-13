using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace HakaTech.Portal.Services;

public class GuacamoleService : IGuacamoleService
{
    private readonly GuacamoleSettings _settings;
    private readonly IDataProtector    _protector;
    private readonly ILogger<GuacamoleService> _logger;

    public GuacamoleService(
        IOptions<GuacamoleSettings>   options,
        IDataProtectionProvider       dpProvider,
        ILogger<GuacamoleService>     logger)
    {
        _settings = options.Value;
        _protector = dpProvider.CreateProtector("RemoteDesktopPasswords");
        _logger    = logger;
    }

    // ── Julkinen rajapinta ───────────────────────────────────────────

    public string? BuildConnectionUrl(RemoteDesktopConnection connection, string userEmail)
    {
        if (!_settings.IsConfigured)
            return null;

        // 1. Pura salasana suojauksesta
        string? clearPassword = null;
        if (!string.IsNullOrEmpty(connection.EncryptedPassword))
        {
            try
            {
                clearPassword = _protector.Unprotect(connection.EncryptedPassword);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Salasanan purku epäonnistui yhteydelle {Id}.", connection.Id);
            }
        }

        // 2. Rakenna JSON-payload (Guacamole JSON Auth -formaatti)
        long expiresMs = DateTimeOffset.UtcNow
            .AddMinutes(_settings.TokenExpiryMinutes)
            .ToUnixTimeMilliseconds();

        var parameters = BuildParameters(connection, clearPassword);

        var payload = new
        {
            username = userEmail,
            expires  = expiresMs,
            connections = new Dictionary<string, object>
            {
                [connection.Name] = new
                {
                    protocol   = connection.Protocol.ToString().ToLowerInvariant(),
                    parameters = parameters
                }
            }
        };

        string json = JsonSerializer.Serialize(payload);

        // 3. Salaa AES-128-CBC; avain = ensimmäiset 16 UTF8-tavua secretistä
        byte[] keyBytes = Encoding.UTF8.GetBytes(
            _settings.JsonSecretKey.PadRight(16)[..16]);

        string data = EncryptToBase64(json, keyBytes);

        return $"{_settings.BaseUrl!.TrimEnd('/')}/?data={Uri.EscapeDataString(data)}";
    }

    public string ProtectPassword(string plaintext) =>
        _protector.Protect(plaintext);

    // ── Yksityiset apumetodit ────────────────────────────────────────

    private static Dictionary<string, string> BuildParameters(
        RemoteDesktopConnection c, string? clearPassword)
    {
        var p = new Dictionary<string, string>
        {
            ["hostname"] = c.Hostname,
            ["port"]     = c.Port.ToString()
        };

        if (!string.IsNullOrEmpty(c.Username))
            p["username"] = c.Username;

        if (!string.IsNullOrEmpty(clearPassword))
            p["password"] = clearPassword;

        if (c.Protocol == RemoteDesktopProtocol.Rdp)
        {
            p["ignore-cert"] = c.IgnoreCert ? "true" : "false";
            p["security"]    = c.Security;
        }

        return p;
    }

    /// <summary>
    /// Guacamole JSON auth: AES-128-CBC, PKCS7, 16-tavu random IV.
    /// IV liitetään salatun datan eteen ennen base64-koodausta.
    /// </summary>
    private static string EncryptToBase64(string json, byte[] key)
    {
        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key     = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        byte[] plainBytes  = Encoding.UTF8.GetBytes(json);
        byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // IV (16 tavua) + salattu data
        byte[] result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV,        0, result, 0,             aes.IV.Length);
        Buffer.BlockCopy(cipherBytes,   0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }
}
