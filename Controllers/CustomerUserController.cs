using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using HakaTech.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

/// <summary>
/// Asiakasyrityksen pääkäyttäjä hallitsee oman yrityksensä käyttäjiä.
/// Admin pääsee kaikille yrityksille.
/// </summary>
[Authorize]
public class CustomerUserController : Controller
{
    private readonly ApplicationDbContext         _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService                _audit;

    public CustomerUserController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager,
        IAuditService                audit)
    {
        _db          = db;
        _userManager = userManager;
        _audit       = audit;
    }

    // ── GET /CustomerUser  tai  /CustomerUser?customerId=5 ──────────
    public async Task<IActionResult> Index(int? customerId)
    {
        var me      = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");

        // Selvitä mille yritykselle näytetään
        int? targetCustomerId = isAdmin ? (customerId ?? me?.CustomerId) : me?.CustomerId;

        if (!isAdmin && me?.IsCustomerAdmin != true)
            return Forbid();

        if (targetCustomerId is null)
            return isAdmin
                ? RedirectToAction("Index", "Customer")  // admin: valitse asiakas ensin
                : Forbid();

        var customer = await _db.Customers.FindAsync(targetCustomerId.Value);
        if (customer is null) return NotFound();

        var users = await _db.Users
            .Where(u => u.CustomerId == targetCustomerId.Value)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        ViewBag.Customer = customer;
        ViewBag.IsAdmin  = isAdmin;
        return View(users);
    }

    // ── GET /CustomerUser/Create?customerId=5 ───────────────────────
    public async Task<IActionResult> Create(int customerId)
    {
        var me      = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");

        if (!isAdmin && (me?.IsCustomerAdmin != true || me.CustomerId != customerId))
            return Forbid();

        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is null) return NotFound();

        ViewBag.Customer = customer;
        return View(new CustomerUserFormViewModel { CustomerId = customerId });
    }

    // ── POST /CustomerUser/Create ────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerUserFormViewModel model)
    {
        var me      = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");

        if (!isAdmin && (me?.IsCustomerAdmin != true || me.CustomerId != model.CustomerId))
            return Forbid();

        var customer = await _db.Customers.FindAsync(model.CustomerId);
        if (customer is null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.Customer = customer;
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName        = model.Email,
            Email           = model.Email,
            FullName        = model.FullName,
            CustomerId      = model.CustomerId,
            IsCustomerAdmin = model.IsCustomerAdmin,
            EmailConfirmed  = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            ViewBag.Customer = customer;
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, "Customer");
        await _audit.LogAsync("UserCreated", "User", user.Id,
            $"{model.Email} yritykselle {customer.CompanyName}");

        TempData["SuccessMessage"] = $"Käyttäjä {model.Email} luotu.";
        return RedirectToAction(nameof(Index), new { customerId = model.CustomerId });
    }

    // ── GET /CustomerUser/Edit/userId ────────────────────────────────
    public async Task<IActionResult> Edit(string id)
    {
        var me      = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");

        var target = await _userManager.FindByIdAsync(id);
        if (target is null) return NotFound();

        if (!isAdmin && (me?.IsCustomerAdmin != true || me.CustomerId != target.CustomerId))
            return Forbid();
        if (target.Id == me?.Id && !isAdmin)
            return Forbid(); // ei voi muokata itseään CustomerAdmin-toiminnolla

        ViewBag.IsAdmin = isAdmin;
        return View(new CustomerUserEditViewModel
        {
            UserId          = target.Id,
            FullName        = target.FullName,
            Email           = target.Email ?? "",
            IsCustomerAdmin = target.IsCustomerAdmin,
            CustomerId      = target.CustomerId ?? 0
        });
    }

    // ── POST /CustomerUser/Edit ──────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CustomerUserEditViewModel model)
    {
        var me      = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");

        var target = await _userManager.FindByIdAsync(model.UserId);
        if (target is null) return NotFound();

        if (!isAdmin && (me?.IsCustomerAdmin != true || me.CustomerId != target.CustomerId))
            return Forbid();

        if (!ModelState.IsValid)
        {
            ViewBag.IsAdmin = isAdmin;
            return View(model);
        }

        target.FullName        = model.FullName;
        target.IsCustomerAdmin = model.IsCustomerAdmin;

        // Sähköpostiosoitteen vaihto
        if (!string.Equals(target.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            target.UserName = model.Email;
            target.Email    = model.Email;
        }

        await _userManager.UpdateAsync(target);
        await _audit.LogAsync("UserUpdated", "User", target.Id, $"Muokkasi: {target.Email}");

        TempData["SuccessMessage"] = $"Käyttäjä {target.Email} päivitetty.";
        return RedirectToAction(nameof(Index), new { customerId = target.CustomerId });
    }

    // ── POST /CustomerUser/Delete ────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var me      = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");

        var target = await _userManager.FindByIdAsync(id);
        if (target is null) return NotFound();

        if (!isAdmin && (me?.IsCustomerAdmin != true || me.CustomerId != target.CustomerId))
            return Forbid();

        if (target.Id == me?.Id)
        {
            TempData["ErrorMessage"] = "Et voi poistaa omaa käyttäjätiliäsi.";
            return RedirectToAction(nameof(Index), new { customerId = target.CustomerId });
        }

        int? cid   = target.CustomerId;
        string email = target.Email ?? target.Id;

        await _userManager.DeleteAsync(target);
        await _audit.LogAsync("UserDeleted", "User", id, $"Poistettu: {email}");

        TempData["SuccessMessage"] = $"Käyttäjä {email} poistettu.";
        return RedirectToAction(nameof(Index), new { customerId = cid });
    }
}
