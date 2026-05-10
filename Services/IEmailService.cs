namespace HakaTech.Portal.Services;

/// <summary>
/// Sähköpostien lähetyspalvelun rajapinta. Konkreettinen toteutus
/// (esim. <see cref="SmtpEmailService"/>) hoitaa varsinaisen lähettämisen.
/// </summary>
public interface IEmailService
{
    /// <summary>Lähettää HTML-muotoisen sähköpostin annettuun osoitteeseen.</summary>
    /// <param name="toEmail">Vastaanottajan sähköpostiosoite.</param>
    /// <param name="subject">Otsikkorivi.</param>
    /// <param name="htmlMessage">Viestin runko HTML:nä.</param>
    Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
}
