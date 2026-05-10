namespace HakaTech.Portal.Services;

/// <summary>
/// HTML:n puhdistuspalvelu. Poistaa käyttäjän syötteestä mahdolliset
/// haitalliset elementit (esim. &lt;script&gt;-tagit), jotta XSS-hyökkäykset
/// estyvät. Käytetään aina kun käyttäjän syöttämää HTML:ää tallennetaan
/// tai näytetään (esim. tietopankin artikkelit, tiedotteiden sisältö).
/// </summary>
public interface IHtmlSanitizerService
{
    /// <summary>
    /// Puhdistaa annetun HTML-merkkijonon sallimalla vain turvalliset
    /// elementit ja attribuutit. Tyhjälle syötteelle palauttaa tyhjän merkkijonon.
    /// </summary>
    string Sanitize(string? html);
}
