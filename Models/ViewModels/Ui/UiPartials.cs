using Microsoft.AspNetCore.Html;

namespace HakaTech.Portal.Models.ViewModels.Ui;

// ─────────────────────────────────────────────────────────────────────
// Linear/Vercel-tyylisten UI-primitiivien mallit. Nämä ovat yksinkertaisia
// data-luokkia, jotka annetaan partial-näkymille (Razor _Partials/...) ja
// joiden perusteella render rakentuu yhdenmukaiseksi koko portaalissa.
// Käyttötapa: <partial name="_Button" model="new ButtonModel { ... }" />
// ─────────────────────────────────────────────────────────────────────

/// <summary>Painikkeen visuaalinen variantti.</summary>
public enum ButtonVariant
{
    Primary,  // pääpainike — sininen täyttö
    Subtle,   // hiljainen — vaalea reuna
    Ghost,    // läpinäkyvä — vain teksti
    Danger    // vaarallinen toiminto — punainen
}

/// <summary>Painikkeen koko (Sm = pieni, Md = normaali).</summary>
public enum ButtonSize    { Sm, Md }

/// <summary>Painikkeen mallidata. Käytetään _Button.cshtml-partialin kanssa.</summary>
public sealed class ButtonModel
{
    public ButtonVariant Variant { get; init; } = ButtonVariant.Subtle;
    public ButtonSize    Size    { get; init; } = ButtonSize.Md;

    /// <summary>Bootstrap-icons -luokka (esim. "bi-plus-lg"), näkyy tekstin vasemmalla puolella.</summary>
    public string?       Icon    { get; init; }
    public string        Text    { get; init; } = "";

    /// <summary>Jos asetettu, render &lt;a&gt;-tagina tähän osoitteeseen.</summary>
    public string?       Href    { get; init; }

    /// <summary>HTML-button-tyyppi: "submit" tai "button". Käytetään vain jos Href on null.</summary>
    public string?       Type    { get; init; }

    /// <summary>HTML form-attribuutti: viittaa lomakkeen id:hen kun submit-nappi on lomakkeen ulkopuolella.</summary>
    public string?       Form    { get; init; }
    public string?       Id      { get; init; }
    public string?       CssClass { get; init; }
    public string?       Title   { get; init; }

    /// <summary>Vapaat HTML-attribuutit (esim. data-confirm).</summary>
    public Dictionary<string, string>? Attrs { get; init; }
}

/// <summary>Korttilaatikon mallidata _Card.cshtml-partialille.</summary>
public sealed class CardModel
{
    public string?      Title         { get; init; }
    public string?      Subtitle      { get; init; }
    public string?      Icon          { get; init; }

    /// <summary>Otsikon oikealla reunalla näkyvät toimintapainikkeet.</summary>
    public IHtmlContent? HeaderActions { get; init; }

    /// <summary>Kortin pääsisältö.</summary>
    public IHtmlContent  Body          { get; init; } = HtmlString.Empty;

    /// <summary>Vapaaehtoinen alaosan sisältö.</summary>
    public IHtmlContent? Footer        { get; init; }

    /// <summary>Lisä-CSS-luokat. Esim. "flat" poistaa varjon.</summary>
    public string?      CssClass      { get; init; }

    /// <summary>Tiivistetty marginaali — säästää tilaa ahtaissa paikoissa.</summary>
    public bool         Dense         { get; init; }
}

/// <summary>Tilan visuaalinen sävy (esim. tiketin tila-merkki).</summary>
public enum StatusTone { Neutral, Info, Success, Warning, Danger, Purple }

/// <summary>Pyöristetty merkki tilaa tai tagia varten — esim. "Avoin" tai "Maksettu".</summary>
public sealed class StatusBadgeModel
{
    public string     Label   { get; init; } = "";
    public StatusTone Tone    { get; init; } = StatusTone.Neutral;

    /// <summary>Piilota värillinen pallo tekstin edestä.</summary>
    public bool       NoDot   { get; init; }
    public string?    Icon    { get; init; }
}

/// <summary>
/// Yksittäinen lomakekenttä (label + input + virheviesti).
/// Käytetään yhdenmukaisuuden vuoksi kaikissa lomakkeissa.
/// </summary>
public sealed class FormFieldModel
{
    /// <summary>Mallin kentän nimi (esim. "Email"), käytetään ModelState-virheiden hakemiseen.</summary>
    public string  For         { get; init; } = "";

    public string? Label       { get; init; }
    public string? Hint        { get; init; }
    public string? Icon        { get; init; }

    /// <summary>HTML-input-tyyppi: text/number/email/password/date/datetime-local.</summary>
    public string  InputType   { get; init; } = "text";
    public string? Placeholder { get; init; }
    public string? Value       { get; init; }
    public bool    Required    { get; init; }
    public bool    Disabled    { get; init; }

    /// <summary>True = renderöi &lt;textarea&gt; input:n sijaan.</summary>
    public bool    Textarea    { get; init; }
    public int?    Rows        { get; init; }
    public int?    MaxLength   { get; init; }
    public string? CssClass    { get; init; }
    public Dictionary<string, string>? Attrs { get; init; }
}

/// <summary>
/// Sivun yläpalkki: leivänmurut + otsikko + alaotsikko + toimintapainikkeet.
/// Käytetään lähes kaikilla sivuilla yhtenäisyyden vuoksi.
/// </summary>
public sealed class PageHeaderModel
{
    /// <summary>Leivänmurujen polku, esim. ("Tiketit", "/Ticket"), ("Uusi tiketti", null).</summary>
    public List<(string Label, string? Href)> Breadcrumbs { get; init; } = new();

    public string        Title    { get; init; } = "";
    public string?       Subtitle { get; init; }
    public string?       Icon     { get; init; }
    public IHtmlContent? Actions  { get; init; }

    /// <summary>Otsikon vieressä näkyvä merkki (esim. tiketin tila).</summary>
    public IHtmlContent? TitleBadge { get; init; }
}

/// <summary>Tyhjän tilan ohje — kun listalla ei ole vielä tietoja.</summary>
public sealed class EmptyStateModel
{
    public string        Icon    { get; init; } = "bi-inbox";
    public string        Title   { get; init; } = "";
    public string?       Body    { get; init; }

    /// <summary>Toiminta, esim. "Luo ensimmäinen tiketti" -painike.</summary>
    public IHtmlContent? Action  { get; init; }
}

/// <summary>
/// Segmentoitu kontrolli (vaihtoehdot vierekkäin). Käytetään esim.
/// suodatuksessa: "Kaikki | Avoimet | Suljetut".
/// </summary>
public sealed class SegmentedModel
{
    /// <summary>HTML-name-attribuutti, käytetään arvon välittämiseen lomakkeessa.</summary>
    public string  Name     { get; init; } = "";

    /// <summary>Tällä hetkellä valittuna oleva arvo.</summary>
    public string? Selected { get; init; }

    /// <summary>Valittavissa olevat vaihtoehdot.</summary>
    public List<(string Value, string Label, string? Icon)> Options { get; init; } = new();

    /// <summary>True = venyttää koko leveyteen.</summary>
    public bool    Full     { get; init; }
    public string? CssClass { get; init; }
}
