using System.Globalization;
using System.Text;
using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using HakaTech.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

[Authorize(Roles = "Admin")]
public class ReportController : Controller
{
    private readonly ApplicationDbContext _db;

    public ReportController(ApplicationDbContext db)
    {
        _db = db;
    }

    private static readonly CultureInfo FI = new("fi-FI");

    // ── GET /Report ──────────────────────────────────────────────────
    public async Task<IActionResult> Index(int weeksBack = 12, int monthsBack = 12)
    {
        weeksBack  = Math.Clamp(weeksBack,  4, 52);
        monthsBack = Math.Clamp(monthsBack, 3, 24);

        var vm = new ReportIndexViewModel
        {
            WeeksBack  = weeksBack,
            MonthsBack = monthsBack,
            WeeklyStats  = await BuildWeeklyStats(weeksBack),
            MonthlyStats = await BuildMonthlyStats(monthsBack)
        };

        // Tila-jakauma
        var counts = await _db.Tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var c in counts)
        {
            switch (c.Status)
            {
                case TicketStatus.Open:            vm.CountOpen            = c.Count; break;
                case TicketStatus.InProgress:      vm.CountInProgress      = c.Count; break;
                case TicketStatus.WaitingCustomer: vm.CountWaitingCustomer = c.Count; break;
                case TicketStatus.Resolved:        vm.CountResolved        = c.Count; break;
                case TicketStatus.Closed:          vm.CountClosed          = c.Count; break;
            }
        }

        vm.TotalBilledExcl = vm.MonthlyStats.Sum(s => s.TotalExcl);
        vm.TotalBilledIncl = vm.MonthlyStats.Sum(s => s.TotalIncl);
        vm.TotalPaid       = vm.MonthlyStats.Sum(s => s.PaidAmount);

        return View(vm);
    }

    // ── GET /Report/ExportTicketsCsv ─────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ExportTicketsCsv()
    {
        var tickets = await _db.Tickets
            .Include(t => t.Customer)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Id;Otsikko;Tila;Prioriteetti;Kategoria;Asiakas;Luonut;Vastuuhenkilö;Luotu;Ratkaistu");

        foreach (var t in tickets)
        {
            sb.AppendLine(string.Join(";",
                t.Id,
                Q(t.Title),
                t.Status,
                t.Priority,
                t.Category,
                Q(t.Customer?.CompanyName ?? ""),
                Q(t.CreatedByUser?.Email ?? ""),
                Q(t.AssignedToUser?.Email ?? ""),
                t.CreatedAt.ToString("dd.MM.yyyy", FI),
                t.ResolvedAt?.ToString("dd.MM.yyyy", FI) ?? ""
            ));
        }

        return CsvFile(sb, $"tiketit_{DateTime.Today:yyyyMMdd}.csv");
    }

    // ── GET /Report/ExportBillingCsv ─────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ExportBillingCsv()
    {
        var invoices = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Laskunro;Asiakas;Tila;Laskun pvm;Eräpäivä;Maksettu;Veroton (€);ALV (€);Yhteensä (€)");

        foreach (var inv in invoices)
        {
            sb.AppendLine(string.Join(";",
                inv.InvoiceNumber,
                Q(inv.Customer?.CompanyName ?? ""),
                inv.Status,
                inv.InvoiceDate.ToString("dd.MM.yyyy", FI),
                inv.DueDate.ToString("dd.MM.yyyy", FI),
                inv.PaidAt?.ToString("dd.MM.yyyy", FI) ?? "",
                inv.SubTotal.ToString("F2", FI),
                inv.VatAmount.ToString("F2", FI),
                inv.TotalAmount.ToString("F2", FI)
            ));
        }

        return CsvFile(sb, $"laskutus_{DateTime.Today:yyyyMMdd}.csv");
    }

    // ── GET /Report/ExportBillingPdf ─────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ExportBillingPdf(int monthsBack = 12)
    {
        monthsBack = Math.Clamp(monthsBack, 1, 24);
        var stats  = await BuildMonthlyStats(monthsBack);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
            .AddMonths(-(monthsBack - 1));
        string period  = $"{monthStart.ToString("MMMM yyyy", FI)} – {DateTime.Now.ToString("MMMM yyyy", FI)}";

        var doc      = new BillingReportPdfDocument(stats, period);
        var pdfBytes = QuestPDF.Fluent.GenerateExtensions.GeneratePdf(doc);

        return File(pdfBytes, "application/pdf", $"laskutusraportti_{DateTime.Today:yyyyMMdd}.pdf");
    }

    // ── Apumetodit ───────────────────────────────────────────────────

    private async Task<List<WeeklyTicketStat>> BuildWeeklyStats(int weeksBack)
    {
        var start = DateTime.UtcNow.Date.AddDays(-(weeksBack * 7));

        var tickets = await _db.Tickets
            .Where(t => t.CreatedAt >= start || (t.ResolvedAt.HasValue && t.ResolvedAt >= start))
            .Select(t => new { t.CreatedAt, t.ResolvedAt })
            .ToListAsync();

        var stats = new List<WeeklyTicketStat>();
        var cur   = start;

        while (cur <= DateTime.UtcNow.Date)
        {
            var end     = cur.AddDays(7);
            int weekNum = ISOWeek.GetWeekOfYear(cur);
            stats.Add(new WeeklyTicketStat
            {
                Label    = $"Vk {weekNum} / {cur.Year}",
                Created  = tickets.Count(t => t.CreatedAt >= cur && t.CreatedAt < end),
                Resolved = tickets.Count(t => t.ResolvedAt.HasValue
                                           && t.ResolvedAt >= cur && t.ResolvedAt < end)
            });
            cur = end;
        }

        return stats;
    }

    private async Task<List<MonthlyBillingStat>> BuildMonthlyStats(int monthsBack)
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
            .AddMonths(-(monthsBack - 1));

        var invoices = await _db.Invoices
            .Include(i => i.Lines)
            .Where(i => i.InvoiceDate >= monthStart)
            .ToListAsync();

        var stats = new List<MonthlyBillingStat>();
        var d     = monthStart;

        while (d.Year < DateTime.UtcNow.Year
            || (d.Year == DateTime.UtcNow.Year && d.Month <= DateTime.UtcNow.Month))
        {
            var month = invoices
                .Where(i => i.InvoiceDate.Year == d.Year && i.InvoiceDate.Month == d.Month)
                .ToList();

            stats.Add(new MonthlyBillingStat
            {
                Label        = d.ToString("MMMM yyyy", FI),
                InvoiceCount = month.Count,
                TotalExcl    = month.Sum(i => i.SubTotal),
                TotalVat     = month.Sum(i => i.VatAmount),
                TotalIncl    = month.Sum(i => i.TotalAmount),
                PaidAmount   = month.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.TotalAmount)
            });

            d = d.AddMonths(1);
        }

        return stats;
    }

    private static FileContentResult CsvFile(StringBuilder sb, string fileName)
    {
        // UTF-8 BOM jotta Excel avaa oikein
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new FileContentResult(bytes, "text/csv;charset=utf-8")
        {
            FileDownloadName = fileName
        };
    }

    // Lainausmerkkisuojaus CSV-arvoille
    private static string Q(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";
}
