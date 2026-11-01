using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBank.Data;
using SmartBank.Models;
using SmartBank.ViewModels;

namespace SmartBank.Controllers;

[Authorize]
public class ProfileController : BaseController
{
    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db)
    {
        _db = db;
    }

    // ---------- Profile aur CNIC / Contact information ----------

    public async Task<IActionResult> Index()
    {
        var customer = await _db.Customers.FindAsync(CurrentCustomerId);
        if (customer == null) return NotFound();
        return View(customer);
    }

    // ---------- Profile Update (email aur mobile). CNIC aur naam bank ke paas verify hoke hi badalte hain. ----------

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var customer = await _db.Customers.FindAsync(CurrentCustomerId);
        if (customer == null) return NotFound();

        return View(new ProfileEditViewModel { Email = customer.Email, Phone = customer.Phone });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProfileEditViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var customer = await _db.Customers.FindAsync(CurrentCustomerId);
        if (customer == null) return NotFound();

        string email = vm.Email.Trim().ToLower();

        bool taken = await _db.Customers.AnyAsync(c =>
            c.Id != customer.Id && (c.Email == email || c.Phone == vm.Phone));
        if (taken)
        {
            ModelState.AddModelError("", "This email or mobile number is already used by another customer.");
            return View(vm);
        }

        customer.Email = email;
        customer.Phone = vm.Phone;
        Bank.Log(_db, customer.Id, "ProfileUpdate", "Email / mobile updated");
        Bank.Log(_db, customer.Id, "Alert", "Your profile contact details were updated.");
        await _db.SaveChangesAsync();

        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Change Password ----------

    [HttpGet]
    public IActionResult ChangePassword() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var customer = await _db.Customers.FindAsync(CurrentCustomerId);
        if (customer == null) return NotFound();

        if (!Bank.VerifySecret(customer, customer.PasswordHash, vm.CurrentPassword))
        {
            ModelState.AddModelError("CurrentPassword", "Current password is wrong.");
            return View(vm);
        }

        customer.PasswordHash = Bank.HashSecret(customer, vm.NewPassword);
        Bank.Log(_db, customer.Id, "PasswordChange", "Password changed");
        Bank.Notify(_db, customer.Id, "Email", "Your SmartBank password was changed. If this was not you, contact the bank.");
        await _db.SaveChangesAsync();

        TempData["Success"] = "Password changed.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Login History (audit log se) ----------

    public async Task<IActionResult> LoginHistory()
    {
        var list = await _db.AuditLogs
            .Where(a => a.CustomerId == CurrentCustomerId && (a.Action == "Login" || a.Action == "LoginFailed"))
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .ToListAsync();
        return View(list);
    }
}
