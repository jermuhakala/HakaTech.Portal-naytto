using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

[Authorize]
public class CustomerController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CustomerController> _logger;

    public CustomerController(ApplicationDbContext db, ILogger<CustomerController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── GET /Customer ────────────────────────────────────────────────
    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(c =>
                c.CompanyName.Contains(search) ||
                c.BusinessId.Contains(search) ||
                c.ContactEmail.Contains(search));
        }

        var customers = await query
            .OrderBy(c => c.CompanyName)
            .ToListAsync();

        ViewBag.Search = search;
        return View(customers);
    }

    // ── GET /Customer/Details/5 ──────────────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _db.Customers
            .Include(c => c.Tickets.OrderByDescending(t => t.CreatedAt).Take(10))
            .Include(c => c.Invoices.OrderByDescending(i => i.InvoiceDate).Take(10))
            .Include(c => c.Contracts)
            .Include(c => c.Users)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer is null)
            return NotFound();

        return View(customer);
    }

    // ── GET /Customer/Create ─────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        return View(new CustomerFormViewModel());
    }

    // ── POST /Customer/Create ────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Tarkista Y-tunnuksen uniikkius
        if (await _db.Customers.AnyAsync(c => c.BusinessId == model.BusinessId))
        {
            ModelState.AddModelError(nameof(model.BusinessId),
                "Y-tunnus on jo käytössä toisella asiakkaalla.");
            return View(model);
        }

        var customer = new Customer
        {
            CompanyName = model.CompanyName,
            BusinessId = model.BusinessId,
            ContactEmail = model.ContactEmail,
            Phone = model.Phone,
            Address = model.Address,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Asiakas {Name} luotu (Id={Id}).", customer.CompanyName, customer.Id);
        TempData["SuccessMessage"] = $"Asiakas \"{customer.CompanyName}\" luotu onnistuneesti.";
        return RedirectToAction(nameof(Index));
    }

    // ── GET /Customer/Edit/5 ─────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null)
            return NotFound();

        return View(new CustomerFormViewModel
        {
            Id = customer.Id,
            CompanyName = customer.CompanyName,
            BusinessId = customer.BusinessId,
            ContactEmail = customer.ContactEmail,
            Phone = customer.Phone,
            Address = customer.Address,
            IsActive = customer.IsActive
        });
    }

    // ── POST /Customer/Edit/5 ────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(model);

        // Tarkista Y-tunnuksen uniikkius (poislukien itsensä)
        if (await _db.Customers.AnyAsync(c => c.BusinessId == model.BusinessId && c.Id != id))
        {
            ModelState.AddModelError(nameof(model.BusinessId),
                "Y-tunnus on jo käytössä toisella asiakkaalla.");
            return View(model);
        }

        var customer = await _db.Customers.FindAsync(id);
        if (customer is null)
            return NotFound();

        customer.CompanyName = model.CompanyName;
        customer.BusinessId = model.BusinessId;
        customer.ContactEmail = model.ContactEmail;
        customer.Phone = model.Phone;
        customer.Address = model.Address;
        customer.IsActive = model.IsActive;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Asiakas {Name} päivitetty (Id={Id}).", customer.CompanyName, customer.Id);
        TempData["SuccessMessage"] = $"Asiakkaan \"{customer.CompanyName}\" tiedot päivitetty.";
        return RedirectToAction(nameof(Details), new { id = customer.Id });
    }

    // ── POST /Customer/Delete/5 ──────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _db.Customers
            .Include(c => c.Tickets)
            .Include(c => c.Invoices)
            .Include(c => c.Contracts)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer is null)
            return NotFound();

        if (customer.Tickets.Any() || customer.Invoices.Any() || customer.Contracts.Any())
        {
            TempData["ErrorMessage"] =
                "Asiakasta ei voi poistaa, koska sillä on avoimia tikettejä, laskuja tai sopimuksia. " +
                "Aseta asiakas epäaktiiviseksi sen sijaan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Asiakas {Name} poistettu (Id={Id}).", customer.CompanyName, id);
        TempData["SuccessMessage"] = $"Asiakas \"{customer.CompanyName}\" poistettu.";
        return RedirectToAction(nameof(Index));
    }
}
