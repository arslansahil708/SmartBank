using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBank.Data;
using SmartBank.Models;
using SmartBank.ViewModels;

namespace SmartBank.Controllers;

[Authorize]
public class ServicesController : BaseController
{
    private readonly AppDbContext _db;

    // Customer in cheezon ki request kar sakta hai
    private static readonly string[] RequestTypes =
        { "Cheque Book", "Stop Cheque", "Change Address", "Debit Card", "Account Maintenance" };

    public ServicesController(AppDbContext db)
    {
        _db = db;
    }

    // Form mein user ko batane ke liye ke Details mein kya likhna hai
    private static string Hint(string type)
    {
        return type switch
        {
            "Cheque Book" => "How many leaves? (10, 25 or 50)",
            "Stop Cheque" => "Cheque number to stop, and the reason",
            "Change Address" => "Write your complete new address",
            "Debit Card" => "Optional: card type or any note",
            _ => "Describe your request (for example: close account, upgrade account, update details)"
        };
    }

    // ---------- Services ka main page + meri requests ----------

    public async Task<IActionResult> Index()
    {
        var list = await _db.ServiceRequests
            .Include(r => r.Account)
            .Where(r => r.CustomerId == CurrentCustomerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        return View(list);
    }

    // ---------- Naye request ka form (Cheque Book, Stop Cheque, Address, Debit Card, Maintenance) ----------

    [HttpGet]
    public async Task<IActionResult> New(string type)
    {
        if (!RequestTypes.Contains(type)) return NotFound();

        await LoadAccounts(_db);
        ViewBag.Hint = Hint(type);
        return View(new ServiceRequestViewModel { Type = type });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> New(ServiceRequestViewModel vm)
    {
        if (!RequestTypes.Contains(vm.Type)) return NotFound();

        await LoadAccounts(_db);
        ViewBag.Hint = Hint(vm.Type);

        string details = (vm.Details ?? "").Trim();
        if (vm.Type != "Debit Card" && details.Length == 0)
            ModelState.AddModelError("Details", "Please enter the details.");

        int? accountId = null;
        if (vm.Type != "Change Address")
        {
            // Account zaroori hai aur wo mera apna hona chahiye
            var account = vm.AccountId == null ? null : await MyActiveAccount(_db, vm.AccountId.Value);
            if (account == null)
                ModelState.AddModelError("AccountId", "Please select an account.");
            else
                accountId = account.Id;
        }

        if (vm.Type == "Debit Card" && accountId != null)
        {
            bool hasCard = await _db.DebitCards.AnyAsync(c => c.AccountId == accountId);
            bool pending = await _db.ServiceRequests.AnyAsync(r =>
                r.Type == "Debit Card" && r.AccountId == accountId && r.Status == "Pending");
            if (hasCard || pending)
                ModelState.AddModelError("AccountId", "This account already has a debit card or a pending card request.");
        }

        if (!ModelState.IsValid) return View(vm);

        _db.ServiceRequests.Add(new ServiceRequest
        {
            CustomerId = CurrentCustomerId,
            AccountId = accountId,
            Type = vm.Type,
            Details = details
        });
        Bank.Notify(_db, CurrentCustomerId, "SMS", vm.Type + " request received. Our team will process it soon.");
        Bank.Log(_db, CurrentCustomerId, "ServiceRequest", vm.Type);
        await _db.SaveChangesAsync();

        TempData["Success"] = vm.Type + " request submitted.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Debit Cards: Block / Unblock ----------

    public async Task<IActionResult> Cards()
    {
        var cards = await _db.DebitCards
            .Include(c => c.Account)
            .Where(c => c.CustomerId == CurrentCustomerId)
            .ToListAsync();
        return View(cards);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCard(int id)
    {
        var card = await _db.DebitCards.FirstOrDefaultAsync(c => c.Id == id && c.CustomerId == CurrentCustomerId);
        if (card == null) return NotFound();

        card.Status = card.Status == "Active" ? "Blocked" : "Active";

        Bank.Notify(_db, CurrentCustomerId, "SMS", $"Your debit card {card.MaskedNumber} is now {card.Status}.");
        Bank.Log(_db, CurrentCustomerId, "CardStatus", $"{card.MaskedNumber} {card.Status}");
        await _db.SaveChangesAsync();

        TempData["Success"] = "Card is now " + card.Status + ".";
        return RedirectToAction(nameof(Cards));
    }

    // ---------- Digital Transaction Certificate ----------

    [HttpGet]
    public async Task<IActionResult> Certificate()
    {
        await LoadAccounts(_db);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> CertificateResult(int accountId, DateTime fromDate, DateTime toDate)
    {
        var account = await _db.Accounts
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == accountId && a.CustomerId == CurrentCustomerId);
        if (account == null) return NotFound();

        if (toDate < fromDate)
        {
            TempData["Error"] = "'To' date must be after 'From' date.";
            return RedirectToAction(nameof(Certificate));
        }

        DateTime end = toDate.Date.AddDays(1);   // toDate ka poora din shamil
        var txns = await _db.Transactions
            .Where(t => t.AccountId == accountId && t.Date >= fromDate.Date && t.Date < end)
            .ToListAsync();

        var vm = new CertificateViewModel
        {
            Account = account,
            HolderName = account.Customer!.FullName,
            CertificateNo = "DTC-" + DateTime.Now.ToString("yyyyMMddHHmm") + "-" + account.Id,
            FromDate = fromDate.Date,
            ToDate = toDate.Date,
            Count = txns.Count,
            TotalDebit = txns.Where(t => t.Type == "Debit").Sum(t => t.Amount),
            TotalCredit = txns.Where(t => t.Type == "Credit").Sum(t => t.Amount)
        };

        Bank.Log(_db, CurrentCustomerId, "Certificate", vm.CertificateNo);
        await _db.SaveChangesAsync();

        return View(vm);
    }
}
