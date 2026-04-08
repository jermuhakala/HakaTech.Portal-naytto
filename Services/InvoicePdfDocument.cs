using HakaTech.Portal.Models.Domain;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HakaTech.Portal.Services;

public class InvoicePdfDocument : IDocument
{
    private readonly Invoice _invoice;

    public InvoicePdfDocument(Invoice invoice)
    {
        _invoice = invoice;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("HakaTech Oy").FontSize(24).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text("Tietotekniikkatie 1 A");
                column.Item().Text("00100 Helsinki, Finland");
                column.Item().Text("Y-Tunnus: 1234567-8");
            });

            row.ConstantItem(150).AlignRight().Column(column =>
            {
                column.Item().Text("LASKU").FontSize(20).SemiBold();
                column.Item().Text($"Laskunumero: {_invoice.InvoiceNumber}");
                column.Item().Text($"Päiväys: {_invoice.InvoiceDate:dd.MM.yyyy}");
                column.Item().Text($"Eräpäivä: {_invoice.DueDate:dd.MM.yyyy}").SemiBold();
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
        {
            column.Item().PaddingBottom(1, Unit.Centimetre).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Asiakas / Laskutusosoite:").SemiBold();
                    c.Item().Text(_invoice.Customer?.CompanyName ?? "Tuntematon asiakas");
                    c.Item().Text(_invoice.Customer?.ContactEmail ?? "");
                });
            });

            column.Item().Element(ComposeTable);

            if (!string.IsNullOrWhiteSpace(_invoice.Notes))
            {
                column.Item().PaddingTop(25).Column(c => 
                {
                    c.Item().Text("Lisätiedot:").SemiBold();
                    c.Item().Text(_invoice.Notes).FontColor(Colors.Grey.Darken2);
                });
            }
        });
    }

    private void ComposeTable(IContainer container)
    {
        var headerStyle = TextStyle.Default.SemiBold();

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);  // Kuvaus
                columns.RelativeColumn(1);  // Määrä
                columns.RelativeColumn(1);  // À-hinta
                columns.RelativeColumn(1);  // Yhteensä
            });

            // Header
            table.Header(header =>
            {
                header.Cell().Text("Kuvaus").Style(headerStyle);
                header.Cell().AlignRight().Text("Määrä").Style(headerStyle);
                header.Cell().AlignRight().Text("À-hinta").Style(headerStyle);
                header.Cell().AlignRight().Text("Yhteensä").Style(headerStyle);
                header.Cell().ColumnSpan(4).PaddingTop(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
            });

            // Lines
            foreach (var line in _invoice.Lines)
            {
                table.Cell().PaddingVertical(5).Text(line.Description);
                table.Cell().PaddingVertical(5).AlignRight().Text(line.Quantity.ToString("G"));
                table.Cell().PaddingVertical(5).AlignRight().Text(line.UnitPrice.ToString("C"));
                table.Cell().PaddingVertical(5).AlignRight().Text((line.Quantity * line.UnitPrice).ToString("C"));
            }

            // Summary
            table.Cell().ColumnSpan(4).PaddingTop(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);

            table.Cell().ColumnSpan(3).PaddingTop(10).AlignRight().Text("Veroton yhteensä:").SemiBold();
            table.Cell().PaddingTop(10).AlignRight().Text(_invoice.SubTotal.ToString("C"));

            table.Cell().ColumnSpan(3).AlignRight().Text($"ALV ({_invoice.VatRate * 100:0.#} %):").SemiBold();
            table.Cell().AlignRight().Text(_invoice.VatAmount.ToString("C"));

            table.Cell().ColumnSpan(3).AlignRight().Text("Maksettava yhteensä:").FontSize(14).SemiBold();
            table.Cell().AlignRight().Text(_invoice.TotalAmount.ToString("C")).FontSize(14).SemiBold().FontColor(Colors.Blue.Darken2);
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Maksuohjeet: Maksakaa summa tilille ").FontSize(10);
            text.Span("FI12 3456 7890 1234 56").SemiBold().FontSize(10);
            text.Span($" viitteellä {_invoice.InvoiceNumber}.").FontSize(10);
        });
    }
}
