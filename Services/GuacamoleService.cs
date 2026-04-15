using System.Net.Http.Headers;
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
    private readonly HttpClient        _http;
    private readonly ILogger<GuacamoleService> _logger;

    public GuacamoleService(
        IOptions<GuacamoleSettings>   options,
        IDataProtectionProvider       dpProvider,
        HttpClient                    httpClient,
        ILogger<GuacamoleService>     logger)
    {
        _settings = options.Value;
        _protector = dpProvider.CreateProtector("RemoteDesktopPasswords");
        _http      = httpClient;
        _logger    = logger;
    }

    // ── Julkinen rajapinta ───────────────────────────────────────────

    public async Task<string?> BuildConnectionUrlAsync(RemoteDesktopConnection connection)
    {
        if (!_settings.IsConfigured)
        {
            _logger.LogWarning("Guacamole ei ole konfiguroitu (BaseUrl/AdminUsername/AdminPassword puuttuu).");
            return null;
        }

        if (string.IsNullOrWhiteSpace(connection.GuacamoleConnectionId))
        {
            _logger.LogWarning(
                "Yhteys '{Name}' (Id={Id}) ei sisällä GuacamoleConnectionId-arvoa.",
                connection.Name, connection.Id);
            return null;
        }

        // 1. Kirjaudu Guacamoleen admin-tunnuksilla → hae authToken
        string? token = await GetAuthTokenAsync();
        if (token is null) return null;

        // 2. Rakenna client-URL
        //    Guacamole client-identifier = base64( {connectionId}\0c\0{dataSource} )
        string identifier = BuildIdentifier(connection.GuacamoleConnectionId, _settings.DataSource);
        string baseUrl    = _settings.BaseUrl!.TrimEnd('/');

        return $"{baseUrl}/#/client/{identifier}?token={token}";
    }

    public string ProtectPassword(string plaintext) =>
        _protector.Protect(plaintext);

    // ── Yksityiset apumetodit ────────────────────────────────────────

    private async Task<string?> GetAuthTokenAsync()
    {
        string apiUrl = $"{_settings.BaseUrl!.TrimEnd('/')}/api/tokens";

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("username", _settings.AdminUsername!),
            new KeyValuePair<string, string>("password", _settings.AdminPassword!)
        });

        try
        {
            var response = await _http.PostAsync(apiUrl, form);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Guacamole-kirjautuminen epäonnistui: {Status} {Url}",
                    (int)response.StatusCode, apiUrl);
                return null;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("authToken", out var tokenProp))
                return tokenProp.GetString();

            _logger.LogError("Guacamole vastaus ei sisältänyt authToken-kenttää: {Json}", json);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Guacamole REST API -kutsu epäonnistui: {Url}", apiUrl);
            return null;
        }
    }

    /// <summary>
    /// Guacamole client-identifier: base64( connectionId + NUL + "c" + NUL + dataSource )
    /// </summary>
    private static string BuildIdentifier(string connectionId, string dataSource)
    {
        // Muodosta tavu-array: id, 0x00, 'c', 0x00, dataSource
        byte[] idBytes  = Encoding.UTF8.GetBytes(connectionId);
        byte[] dsBytes  = Encoding.UTF8.GetBytes(dataSource);
        byte[] combined = new byte[idBytes.Length + 1 + 1 + 1 + dsBytes.Length];

        int pos = 0;
        Buffer.BlockCopy(idBytes, 0, combined, pos, idBytes.Length); pos += idBytes.Length;
        combined[pos++] = 0x00;   // NUL
        combined[pos++] = (byte)'c';
        combined[pos++] = 0x00;   // NUL
        Buffer.BlockCopy(dsBytes, 0, combined, pos, dsBytes.Length);

        return Convert.ToBase64String(combined);
    }
}
