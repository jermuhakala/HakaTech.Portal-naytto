using Microsoft.AspNetCore.Mvc;

namespace HakaTech.Portal.ViewComponents;

public sealed class KpiCardViewComponent : ViewComponent
{
    public sealed class KpiCardVm
    {
        public string Label       { get; init; } = string.Empty;
        public string Value       { get; init; } = string.Empty;
        public string? Sub        { get; init; }
        public string? Icon       { get; init; }
        public string? Href       { get; init; }
        public string Tone        { get; init; } = "neutral"; // neutral|info|success|warning|danger|purple
        public double[] Trend     { get; init; } = Array.Empty<double>();
        public double? DeltaPct   { get; init; }              // optional (+/-), shown as badge
    }

    public IViewComponentResult Invoke(KpiCardVm model)
        => View(model);
}
