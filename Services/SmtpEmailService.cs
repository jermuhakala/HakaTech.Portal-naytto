using System.Net;
using System.Net.Mail;

namespace HakaTech.Portal.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
    {
        try
        {
            var host = _config["SmtpSettings:Host"] ?? "127.0.0.1";
            var port = _config.GetValue<int?>("SmtpSettings:Port") ?? 25;
            var fromEmail = _config["SmtpSettings:FromEmail"] ?? "noreply@hakatech.fi";

            using var client = new SmtpClient(host, port);

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "HakaTech Asiakastuki"),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Sähköposti lähetetty osoitteeseen {To}. Aihe: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe sähköpostin lähetyksessä osoitteeseen {To}", toEmail);
            // Ohitetaan virhe tuotannossa, jotta tiketin päivitys ei kaadu
        }
    }
}
