using Microsoft.AspNetCore.Html;

namespace HakaTech.Portal.Models.ViewModels.Ui;

// ── Linear/Vercel-tyylin UI-primitiivien mallit ───────────────────

public enum ButtonVariant { Primary, Subtle, Ghost, Danger }
public enum ButtonSize    { Sm, Md }

public sealed class ButtonModel
{
    public ButtonVariant Variant { get; init; } = ButtonVariant.Subtle;
    public ButtonSize    Size    { get; init; } = ButtonSize.Md;
    public string?       Icon    { get; init; }
    public string        Text    { get; init; } = "";
    public string?       Href    { get; init; }
    public string?       Type    { get; init; }  // "submit", "button" – jos Href null
    public string?       Form    { get; init; }  // form-attribuutti submit-napille
    public string?       Id      { get; init; }
    public string?       CssClass { get; init; }
    public string?       Title   { get; init; }
    public Dictionary<string, string>? Attrs { get; init; }
}

public sealed class CardModel
{
    public string?      Title         { get; init; }
    public string?      Subtitle      { get; init; }
    public string?      Icon          { get; init; }
    public IHtmlContent? HeaderActions { get; init; }
    public IHtmlContent  Body          { get; init; } = HtmlString.Empty;
    public IHtmlContent? Footer        { get; init; }
    public string?      CssClass      { get; init; }  // esim. "flat"
    public bool         Dense         { get; init; }
}

public enum StatusTone { Neutral, Info, Success, Warning, Danger, Purple }

public sealed class StatusBadgeModel
{
    public string     Label   { get; init; } = "";
    public StatusTone Tone    { get; init; } = StatusTone.Neutral;
    public bool       NoDot   { get; init; }
    public string?    Icon    { get; init; }
}

public sealed class FormFieldModel
{
    public string  For         { get; init; } = "";    // kentän nimi (ModelState-avain)
    public string? Label       { get; init; }
    public string? Hint        { get; init; }
    public string? Icon        { get; init; }
    public string  InputType   { get; init; } = "text"; // text/number/email/password/date/datetime-local
    public string? Placeholder { get; init; }
    public string? Value       { get; init; }
    public bool    Required    { get; init; }
    public bool    Disabled    { get; init; }
    public bool    Textarea    { get; init; }
    public int?    Rows        { get; init; }
    public int?    MaxLength   { get; init; }
    public string? CssClass    { get; init; }
    public Dictionary<string, string>? Attrs { get; init; }
}

public sealed class PageHeaderModel
{
    public List<(string Label, string? Href)> Breadcrumbs { get; init; } = new();
    public string        Title    { get; init; } = "";
    public string?       Subtitle { get; init; }
    public string?       Icon     { get; init; }
    public IHtmlContent? Actions  { get; init; }
    public IHtmlContent? TitleBadge { get; init; }
}

public sealed class EmptyStateModel
{
    public string        Icon    { get; init; } = "bi-inbox";
    public string        Title   { get; init; } = "";
    public string?       Body    { get; init; }
    public IHtmlContent? Action  { get; init; }
}

public sealed class SegmentedModel
{
    public string  Name     { get; init; } = "";
    public string? Selected { get; init; }
    public List<(string Value, string Label, string? Icon)> Options { get; init; } = new();
    public bool    Full     { get; init; }
    public string? CssClass { get; init; }
}
