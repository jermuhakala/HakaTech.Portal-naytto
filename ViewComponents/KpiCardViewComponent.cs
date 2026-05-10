using Microsoft.AspNetCore.Mvc;

namespace HakaTech.Portal.ViewComponents;

/// <summary>
/// KPI-kortin (Key Performance Indicator) ViewComponent. Näytetään
/// dashboardilla isoina lukuina sekä pienenä trendiviivakaaviona.
/// Esim. "Avoimia tikettejä: 12" + sparkline viimeisen 14 päivän ajalta.
/// </summary>
public sealed class KpiCardViewComponent : ViewComponent
{
    /// <summary>Yhden KPI-kortin näytön malli.</summary>
    public sealed class KpiCardVm
    {
        /// <summary>Kortin otsikko, esim. "Avoimet tiketit".</summary>
        public string Label       { get; init; } = string.Empty;

        /// <summary>Pääarvo isona, esim. "12".</summary>
        public string Value       { get; init; } = string.Empty;

        /// <summary>Pienempi alateksti, esim. "+3 viime viikolla".</summary>
        public string? Sub        { get; init; }

        /// <summary>Bootstrap-icons -luokka (esim. "bi-ticket-detailed").</summary>
        public string? Icon       { get; init; }

        /// <summary>Linkki johon kortti vie kun klikataan. Null = ei klikattava.</summary>
        public string? Href       { get; init; }

        /// <summary>Värisävy: neutral/info/success/warning/danger/purple.</summary>
        public string Tone        { get; init; } = "neutral";

        /// <summary>Sparkline-data (lukusarja viivakaavioon).</summary>
        public double[] Trend     { get; init; } = Array.Empty<double>();

        /// <summary>Vapaaehtoinen muutosprosentti, näkyy värillisenä merkkinä (+/-).</summary>
        public double? DeltaPct   { get; init; }
    }

    public IViewComponentResult Invoke(KpiCardVm model)
        => View(model);
}
