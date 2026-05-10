using Ganss.Xss;

namespace HakaTech.Portal.Services;

/// <summary>
/// HTML-puhdistuspalvelun toteutus, joka pohjautuu Ganss.Xss-kirjastoon.
/// Pitää valkoista listaa sallituista tageista, attribuuteista ja URL-skeemoista —
/// kaikki muu poistetaan automaattisesti.
///
/// Tämä palvelu rekisteröidään singletonina, koska konfiguraatio on
/// muuttumaton ja sanitizer on thread-safe.
/// </summary>
public class HtmlSanitizerService : IHtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();

        // Poistetaan kirjaston oletukset ja luodaan tiukka oma valkoinen lista.
        _sanitizer.AllowedTags.Clear();

        // Sallitut HTML-tagit: tekstinmuotoilu, listat, linkit, otsikot,
        // taulukot, koodit ja perus-blokkielementit. EI esim. <script> tai <iframe>.
        foreach (var tag in new[]
        {
            "p", "br", "strong", "b", "em", "i", "u",
            "ul", "ol", "li",
            "a", "h2", "h3", "h4",
            "code", "pre", "blockquote",
            "table", "thead", "tbody", "tr", "th", "td",
            "img", "span", "div", "hr"
        })
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        // Vain turvalliset attribuutit (esim. ei "onerror" tai "onclick").
        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("href");
        _sanitizer.AllowedAttributes.Add("title");
        _sanitizer.AllowedAttributes.Add("alt");
        _sanitizer.AllowedAttributes.Add("src");
        _sanitizer.AllowedAttributes.Add("class");

        // Sallitut URL-skeemat — erityisesti EI "javascript:" jolla voisi tehdä XSS.
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");
    }

    public string Sanitize(string? html)
        => string.IsNullOrWhiteSpace(html) ? string.Empty : _sanitizer.Sanitize(html);
}
