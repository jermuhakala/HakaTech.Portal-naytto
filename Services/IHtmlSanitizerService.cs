namespace HakaTech.Portal.Services;

public interface IHtmlSanitizerService
{
    string Sanitize(string? html);
}
