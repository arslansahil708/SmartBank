using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBank.Data;
using SmartBank.Models;
using SmartBank.ViewModels;

namespace SmartBank.Controllers;

[Authorize]   // login ke bina is controller mein koi nahi aa sakta
public class AccountController : BaseController
{
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db)
    {
        _db = db;
    }

    // Sirf login user ka apna account, kisi aur ka nahi (id badal kar dekhne se bachata hai)
    private Task<Account?> GetMyAccount(int id)
    {
        return _db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == CurrentCustomerId);
    }

    // ---------- Mere accounts + Balance Enquiry ----------

    public async Task<IActionResult> Index()
    {
        var accounts = await _db.Accounts
            .Where(a => a.CustomerId == CurrentCustomerId)
            .OrderBy(a => a.Id)
            .ToListAsync();

        return View(accounts);
    }

    // ---------- Naya Savings / Current account kholna ----------

    [HttpGet]
    public IActionResult Open() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(string accountType)
    {
        if (accountType != "Savings" && accountType != "Current")
            return BadRequest();

        // Demo project hai, is liye shuru mein Rs 10,000 de dete hain taake transfer test ho sake
        const decimal demoOpeningBalance = 10000;

        var account = new Account
        {
            AccountNumber = Bank.NewAccountNumber(_db),
            AccountType = accountType,
            Balance = demoOpeningBalance,
            CustomerId = CurrentCustomerId
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();   // account.Id chahiye transaction ke liye

        _db.Transactions.Add(new BankTransaction
        {
            AccountId = account.Id,
            Type = "Credit",
            Amount = demoOpeningBalance,
            BalanceAfter = demoOpeningBalance,
            Description = "Opening balance (demo)",
            Reference = Bank.NewReference()
        });
        Bank.Log(_db, CurrentCustomerId, "OpenAccount", accountType + " account " + account.AccountNumber);
        await _db.SaveChangesAsync();

        TempData["Success"] = accountType + " account " + account.AccountNumber + " opened.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Account Details ----------

    public async Task<IActionResult> Details(int id)
    {
        var account = await _db.Accounts
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == CurrentCustomerId);

        if (account == null) return NotFound();
        return View(account);
    }

    // ---------- Mini Statement (aakhri 5 transactions) ----------

    public async Task<IActionResult> MiniStatement(int id)
    {
        var account = await GetMyAccount(id);
        if (account == null) return NotFound();

        var last5 = await _db.Transactions
            .Where(t => t.AccountId == id)
            .OrderByDescending(t => t.Date)
            .Take(5)
            .ToListAsync();

        return View(new StatementViewModel
        {
            Account = account,
            Transactions = last5,
            Year = DateTime.Now.Year
        });
    }

    // ---------- Monthly / Annual Statement ----------
    // month dena hai to monthly, month khali ho to poore saal ka (annual)

    public async Task<IActionResult> Statement(int id, int? year, int? month)
    {
        var account = await GetMyAccount(id);
        if (account == null) return NotFound();

        int y = year ?? DateTime.Now.Year;

        var query = _db.Transactions.Where(t => t.AccountId == id && t.Date.Year == y);
        if (month != null)
            query = query.Where(t => t.Date.Month == month);

        var list = await query.OrderBy(t => t.Date).ToListAsync();

        return View(new StatementViewModel
        {
            Account = account,
            Transactions = list,
            Year = y,
            Month = month
        });
    }
}
