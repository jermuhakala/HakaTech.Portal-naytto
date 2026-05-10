using HakaTech.Portal.Models.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HakaTech.Portal.Services;

/// <summary>
/// Laskutusyhteenvedon PDF-raportti. Näyttää kuukausikohtaiset
/// laskutusluvut taulukkona (veroton, ALV, verollinen, maksettu).
/// Käytetään admin-puolella raporttisivulla "Vie PDF" -toiminnolla.
/// </summary>
public class BillingReportPdfDocument : IDocument
{
    private readonly List<MonthlyBillingStat> _stats;
    private readonly string _periodLabel;

    public BillingReportPdfDocument(List<MonthlyBillingStat> stats, string periodLabel)
    {
        _stats       = stats;
        _periodLabel = periodLabel;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("HakaTech Oy  ·  Tulostettu: ").FontColor(Colors.Grey.Medium);
                t.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm")).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Laskutusyhteenveto")
                    .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                col.Item().Text(_periodLabel)
                    .FontSize(11).FontColor(Colors.Grey.Darken1);
            });
            row.ConstantItem(140).AlignRight().Column(col =>
            {
                col.Item().Text("HakaTech Oy").Bold();
                col.Item().Text("Laskutusraportti").FontColor(Colors.Grey.Medium);
            });
        });

        container.PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Kuukausi
                    columns.RelativeColumn(1); // Laskuja
                    columns.RelativeColumn(2); // Veroton
                    columns.RelativeColumn(2); // ALV
                    columns.RelativeColumn(2); // Yhteensä
                    columns.RelativeColumn(2); // Maksettu
                });

                // Otsikkorivi
                table.Header(header =>
                {
                    static IContainer HeaderCell(IContainer c) =>
                        c.Background(Colors.Blue.Darken2).Padding(5);

                    void H(IContainer c, string text, bool right = false)
                    {
                        var cell = c.Element(HeaderCell);
                        if (right) cell = cell.AlignRight();
                        cell.Text(text).Bold().FontColor(Colors.White);
                    }

                    H(header.Cell(), "Kuukausi");
                    H(header.Cell(), "Laskuja",  right: true);
                    H(header.Cell(), "Veroton",  right: true);
                    H(header.Cell(), "ALV",      right: true);
                    H(header.Cell(), "Yhteensä", right: true);
                    H(header.Cell(), "Maksettu", right: true);
                });

                // Datarivit
                bool alt = false;
                foreach (var stat in _stats)
                {
                    var bg = alt ? Colors.Grey.Lighten4 : Colors.White;
                    alt = !alt;

                    IContainer Cell(IContainer c) => c.Background(bg).Padding(5);

                    table.Cell().Element(Cell).Text(stat.Label);
                    table.Cell().Element(Cell).AlignRight().Text(stat.InvoiceCount.ToString());
                    table.Cell().Element(Cell).AlignRight().Text(stat.TotalExcl.ToString("N2") + " €");
                    table.Cell().Element(Cell).AlignRight().Text(stat.TotalVat.ToString("N2") + " €");
                    table.Cell().Element(Cell).AlignRight().Text(stat.TotalIncl.ToString("N2") + " €");
                    table.Cell().Element(Cell).AlignRight()
                        .Text(stat.PaidAmount > 0 ? stat.PaidAmount.ToString("N2") + " €" : "—");
                }

                // Yhteensä-rivi
                IContainer TotalCell(IContainer c) =>
                    c.Background(Colors.Blue.Lighten4).Padding(5).BorderTop(1).BorderColor(Colors.Blue.Darken1);

                table.Cell().Element(TotalCell).Text("YHTEENSÄ").Bold();
                table.Cell().Element(TotalCell).AlignRight()
                    .Text(_stats.Sum(s => s.InvoiceCount).ToString()).Bold();
                table.Cell().Element(TotalCell).AlignRight()
                    .Text(_stats.Sum(s => s.TotalExcl).ToString("N2") + " €").Bold();
                table.Cell().Element(TotalCell).AlignRight()
                    .Text(_stats.Sum(s => s.TotalVat).ToString("N2") + " €").Bold();
                table.Cell().Element(TotalCell).AlignRight()
                    .Text(_stats.Sum(s => s.TotalIncl).ToString("N2") + " €").Bold();
                table.Cell().Element(TotalCell).AlignRight()
                    .Text(_stats.Sum(s => s.PaidAmount).ToString("N2") + " €").Bold();
            });
        });
    }
}
