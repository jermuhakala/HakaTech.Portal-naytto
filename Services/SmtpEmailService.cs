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
            var port = _config.GetValue<int?>("SmtpSettings:Port") ?? 587;
            var fromEmail = _config["SmtpSettings:FromEmail"] ?? "noreply@hakatech.fi";
            var username = _config["SmtpSettings:Username"];
            var password = _config["SmtpSettings:Password"];
            var enableSsl = _config.GetValue<bool?>("SmtpSettings:EnableSsl") ?? true;

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

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
            _logger.LogInformation("S\u00e4hk\u00f6posti l\u00e4hetetty. Aihe: {Subject}", subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe s\u00e4hk\u00f6postin l\u00e4hetyksess\u00e4");
        }
    }
}
