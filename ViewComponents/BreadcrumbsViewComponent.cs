using Microsoft.AspNetCore.Mvc;

namespace HakaTech.Portal.ViewComponents;

/// <summary>
/// Yläpalkin leivänmurupolun (breadcrumbs) renderöivä ViewComponent.
/// Lukee ViewData["Breadcrumbs"]-listan ja palauttaa siitä HTML-fragmentin.
///
/// Näkymät asettavat polun esim. näin:
///   ViewData["Breadcrumbs"] = new List&lt;(string, string?)&gt; { ("Tiketit", "/Ticket"), ("#123", null) };
///
/// Tyhjä Href tarkoittaa että kohta on nykyinen sivu (ei linkitetä).
/// </summary>
public sealed class BreadcrumbsViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        // Jos breadcrumbsia ei ole asetettu, palautetaan tyhjä lista
        // jolloin näkymä jättää koko palkin renderöimättä.
        var crumbs = ViewData["Breadcrumbs"] as IReadOnlyList<(string Label, string? Href)>
                     ?? Array.Empty<(string, string?)>();
        return View(crumbs);
    }
}
