using System.Net;
using System.Net.Mail;

namespace HakaTech.Portal.Services;

/// <summary>
/// SMTP-pohjainen sähköpostipalvelun toteutus. Lukee SMTP-asetukset
/// (palvelin, portti, käyttäjätunnukset) appsettings.json -tiedostosta
/// avaimen "SmtpSettings" alta.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Lähettää sähköpostin SMTP-palvelimen kautta. Mahdolliset virheet
    /// kirjataan lokiin, mutta poikkeusta ei nosteta — sähköpostin
    /// epäonnistuminen ei saa kaataa muuta toimintoa.
    /// </summary>
    public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
    {
        try
        {
            // Lue SMTP-asetukset konfiguraatiosta. Tarjotaan järkevät oletusarvot,
            // jotta kehitysympäristössä toimii ilman erillistä konfiguraatiota.
            var host = _config["SmtpSettings:Host"] ?? "127.0.0.1";
            var port = _config.GetValue<int?>("SmtpSettings:Port") ?? 587;
            var fromEmail = _config["SmtpSettings:FromEmail"] ?? "noreply@hakatech.fi";
            var username = _config["SmtpSettings:Username"];
            var password = _config["SmtpSettings:Password"];
            var enableSsl = _config.GetValue<bool?>("SmtpSettings:EnableSsl") ?? true;

            // Luodaan SMTP-asiakas. using huolehtii että yhteys suljetaan lopuksi.
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            // Käytetään käyttäjätunnuksia vain jos ne on määritelty
            // (jotkin SMTP-relayt eivät vaadi tunnistautumista).
            if (!string.IsNullOrWhiteSpace(username))
                client.Credentials = new NetworkCredential(username, password);

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "HakaTech Asiakastuki"),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Sähköposti lähetetty. Aihe: {Subject}", subject);
        }
        catch (Exception ex)
        {
            // Lokitetaan virhe mutta jatketaan suoritusta — ei kaadeta sovellusta.
            _logger.LogError(ex, "Virhe sähköpostin lähetyksessä");
        }
    }
}
