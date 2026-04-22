using Microsoft.AspNetCore.Mvc;

namespace HakaTech.Portal.ViewComponents;

/// <summary>
/// Reads ViewData["Breadcrumbs"] (a List&lt;(string Label, string? Href)&gt;) and renders
/// the topbar breadcrumb trail. Views set breadcrumbs via:
///   ViewData["Breadcrumbs"] = new List&lt;(string, string?)&gt; { ("Tickets", "/Ticket"), ("#123", null) };
/// </summary>
public sealed class BreadcrumbsViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var crumbs = ViewData["Breadcrumbs"] as IReadOnlyList<(string Label, string? Href)>
                     ?? Array.Empty<(string, string?)>();
        return View(crumbs);
    }
}
