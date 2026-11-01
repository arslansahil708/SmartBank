using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartBank.Data;
using SmartBank.Models;

namespace SmartBank.Controllers;

// Jo controllers login user ke saath kaam karte hain wo isse inherit karenge
public abstract class BaseController : Controller
{
    // Login ke waqt cookie mein jo Id daali thi wo yahan se milti hai
    protected int CurrentCustomerId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Form ke dropdown ke liye: mere active accounts
    protected async Task LoadAccounts(AppDbContext db)
    {
        var accounts = await db.Accounts
            .Where(a => a.CustomerId == CurrentCustomerId && a.IsActive)
            .OrderBy(a => a.Id)
            .ToListAsync();

        ViewBag.Accounts = new SelectList(
            accounts.Select(a => new { a.Id, Text = $"{a.AccountNumber} ({a.AccountType}) - Rs {a.Balance:N2}" }),
            "Id", "Text");
    }

    // Sirf mera apna active account (kisi aur ka nahi)
    protected Task<Account?> MyActiveAccount(AppDbContext db, int id)
    {
        return db.Accounts.FirstOrDefaultAsync(a =>
            a.Id == id && a.CustomerId == CurrentCustomerId && a.IsActive);
    }

    // Paise bhejne se pehle ka common kaam: customer, source account aur PIN check.
    // error null ho to sab theek hai.
    protected async Task<(Customer? customer, Account? source, string? error)> PrepareMoney(
        AppDbContext db, int sourceAccountId, string pin)
    {
        var customer = await db.Customers.FindAsync(CurrentCustomerId);
        if (customer == null) return (null, null, "Customer not found.");

        var source = await MyActiveAccount(db, sourceAccountId);
        if (source == null) return (customer, null, "Invalid source account.");

        string? pinError = await Bank.CheckPin(db, customer, pin);
        if (pinError != null) return (customer, source, pinError);

        return (customer, source, null);
    }
}
