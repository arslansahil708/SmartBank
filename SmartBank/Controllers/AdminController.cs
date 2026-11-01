using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBank.Data;

using SmartBank.Models;
using SmartBank.ViewModels;

namespace SmartBank.Controllers;

// Sirf Admin role wale is controller mein aa sakte hain
[Authorize(Roles = "Admin")]
public class AdminController : BaseController
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    // ---------- Dashboard ----------

    public async Task<IActionResult> Index()
    {
        ViewBag.Customers = await _db.Customers.CountAsync(c => c.Role == "Customer");
        ViewBag.Accounts = await _db.Accounts.CountAsync();
        ViewBag.TotalDeposits = await _db.Accounts.SumAsync(a => a.Balance);
        ViewBag.PendingLoans = await _db.Loans.CountAsync(l => l.Status == "Pending");
        ViewBag.PendingRequests = await _db.ServiceRequests.CountAsync(r => r.Status == "Pending");
        ViewBag.TodayTransactions = await _db.Transactions.CountAsync(t => t.Date >= DateTime.Today);
        return View();
    }

    // ---------- Customers manage karna ----------

    public async Task<IActionResult> Customers(string? q)
    {
        var query = _db.Customers.Include(c => c.Accounts).Where(c => c.Role == "Customer");

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(c => c.FullName.Contains(q) || c.Email.Contains(q) || c.Phone.Contains(q) || c.Cnic.Contains(q));
        }

        ViewBag.Q = q;
        return View(await query.OrderByDescending(c => c.CreatedAt).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCustomer(int id)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.Role == "Customer");
        if (customer == null) return NotFound();

        customer.IsActive = !customer.IsActive;
        string status = customer.IsActive ? "activated" : "suspended";

        Bank.Log(_db, CurrentCustomerId, "AdminCustomer", $"Customer #{customer.Id} {status}");
        Bank.Notify(_db, customer.Id, "Alert", "Your SmartBank profile was " + status + " by the bank.");
        await _db.SaveChangesAsync();

        TempData["Success"] = customer.FullName + " " + status + ".";
        return RedirectToAction(nameof(Customers));
    }

    // ---------- Accounts activate / suspend ----------

    public async Task<IActionResult> Accounts(string? q)
    {
        var query = _db.Accounts.Include(a => a.Customer).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(a => a.AccountNumber.Contains(q) || a.Customer!.FullName.Contains(q));
        }

        ViewBag.Q = q;
        return View(await query.OrderByDescending(a => a.OpenedAt).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAccount(int id)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id);
        if (account == null) return NotFound();

        account.IsActive = !account.IsActive;
        string status = account.IsActive ? "activated" : "suspended";

        Bank.Log(_db, CurrentCustomerId, "AdminAccount", $"Account {account.AccountNumber} {status}");
        Bank.Notify(_db, account.CustomerId, "Alert", $"Your account {account.AccountNumber} was {status} by the bank.");

        if (!await Bank.Save(_db))
        {
            TempData["Error"] = "Account was changed by someone else. Please try again.";
            return RedirectToAction(nameof(Accounts));
        }

        TempData["Success"] = "Account " + account.AccountNumber + " " + status + ".";
        return RedirectToAction(nameof(Accounts));
    }

    // ---------- Loan applications approve / reject ----------

    public async Task<IActionResult> Loans()
    {
        var loans = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.Account)
            .OrderBy(l => l.Status == "Pending" ? 0 : 1)   // Pending sab se upar
            .ThenByDescending(l => l.CreatedAt)
            .ToListAsync();
        return View(loans);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideLoan(int id, string decision, string? remarks)
    {
        var loan = await _db.Loans
            .Include(l => l.Account)
            .FirstOrDefaultAsync(l => l.Id == id && l.Status == "Pending");
        if (loan == null) return NotFound();

        loan.AdminRemarks = (remarks ?? "").Trim();

        if (decision == "approve")
        {
            if (!loan.Account!.IsActive)
            {
                TempData["Error"] = "Customer's account is not active, cannot disburse the loan.";
                return RedirectToAction(nameof(Loans));
            }

            loan.Status = "Approved";
            loan.ApprovedAt = DateTime.Now;

            // Installment schedule: har mahine ek installment
            for (int i = 1; i <= loan.Months; i++)
            {
                loan.Installments.Add(new LoanInstallment
                {
                    No = i,
                    DueDate = DateTime.Today.AddMonths(i),
                    Amount = loan.MonthlyInstallment
                });
            }

            // Loan ki raqam customer ke account mein
            Bank.Receive(
    _db,
    loan.Account,
    loan.Amount,
    loan.LoanType + " loan disbursed",
    "SmartBank Loans",
    out var loanReference
);
            Bank.Notify(_db, loan.CustomerId, "SMS",
                $"Your {loan.LoanType} loan of Rs {loan.Amount:N2} is approved. Installment Rs {loan.MonthlyInstallment:N2} x {loan.Months} months.");
        }
        else if (decision == "reject")
        {
            loan.Status = "Rejected";
            Bank.Notify(_db, loan.CustomerId, "SMS", $"Your {loan.LoanType} loan application was rejected.");
        }
        else
        {
            return BadRequest();
        }

        Bank.Log(_db, CurrentCustomerId, "AdminLoan", $"Loan #{loan.Id} {loan.Status}");

        if (!await Bank.Save(_db))
        {
            TempData["Error"] = "Account balance changed. Please try again.";
            return RedirectToAction(nameof(Loans));
        }

        TempData["Success"] = "Loan #" + loan.Id + " " + loan.Status.ToLower() + ".";
        return RedirectToAction(nameof(Loans));
    }

    // ---------- Cheque, card aur baqi service requests process karna ----------

    public async Task<IActionResult> Requests()
    {
        var list = await _db.ServiceRequests
            .Include(r => r.Customer)
            .Include(r => r.Account)
            .OrderBy(r => r.Status == "Pending" ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideRequest(int id, string decision, string? remarks)
    {
        var request = await _db.ServiceRequests
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.Id == id && r.Status == "Pending");
        if (request == null) return NotFound();

        request.AdminRemarks = (remarks ?? "").Trim();
        request.ProcessedAt = DateTime.Now;

        if (decision == "approve")
        {
            request.Status = "Approved";

            if (request.Type == "Debit Card" && request.AccountId != null)
            {
                // Demo card: sirf aakhri 4 digit save hote hain
                _db.DebitCards.Add(new DebitCard
                {
                    CustomerId = request.CustomerId,
                    AccountId = request.AccountId.Value,
                    MaskedNumber = "**** **** **** " + Random.Shared.Next(1000, 9999),
                    Expiry = DateTime.Now.AddYears(4).ToString("MM/yy")
                });
            }
            else if (request.Type == "Change Address")
            {
                request.Customer!.Address = request.Details;
            }

            Bank.Notify(_db, request.CustomerId, "SMS", $"Your {request.Type} request was approved.");
        }
        else if (decision == "reject")
        {
            request.Status = "Rejected";
            Bank.Notify(_db, request.CustomerId, "SMS", $"Your {request.Type} request was rejected.");
        }
        else
        {
            return BadRequest();
        }

        Bank.Log(_db, CurrentCustomerId, "AdminRequest", $"Request #{request.Id} {request.Type} {request.Status}");
        await _db.SaveChangesAsync();

        TempData["Success"] = request.Type + " request " + request.Status.ToLower() + ".";
        return RedirectToAction(nameof(Requests));
    }

    // ---------- Transactions dekhna ----------

    public async Task<IActionResult> Transactions(string? q)
    {
        var query = _db.Transactions.Include(t => t.Account).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(t => t.Reference.Contains(q) || t.Account!.AccountNumber.Contains(q) || t.Description.Contains(q));
        }

        ViewBag.Q = q;
        return View(await query.OrderByDescending(t => t.Date).Take(200).ToListAsync());
    }

    // ---------- Audit logs dekhna ----------

    public async Task<IActionResult> AuditLogs(string? q)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(a => a.Action.Contains(q) || a.Details.Contains(q));
        }

        ViewBag.Q = q;
        return View(await query.OrderByDescending(a => a.CreatedAt).Take(200).ToListAsync());
    }

    // ---------- Reports ----------

    public async Task<IActionResult> Reports(DateTime? fromDate, DateTime? toDate)
    {
        DateTime start = (fromDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
        DateTime lastDay = (toDate ?? DateTime.Today).Date;
        DateTime end = lastDay.AddDays(1);   // aakhri din poora shamil

        var txns = _db.Transactions.Where(t => t.Date >= start && t.Date < end);

        var vm = new ReportViewModel
        {
            FromDate = start,
            ToDate = lastDay,
            NewCustomers = await _db.Customers.CountAsync(c => c.Role == "Customer" && c.CreatedAt >= start && c.CreatedAt < end),
            NewAccounts = await _db.Accounts.CountAsync(a => a.OpenedAt >= start && a.OpenedAt < end),
            TransactionCount = await txns.CountAsync(),
            TotalDebit = await txns.Where(t => t.Type == "Debit").SumAsync(t => t.Amount),
            TotalCredit = await txns.Where(t => t.Type == "Credit").SumAsync(t => t.Amount),
            LoansApproved = await _db.Loans.CountAsync(l => l.ApprovedAt != null && l.ApprovedAt >= start && l.ApprovedAt < end),
            LoansAmount = await _db.Loans.Where(l => l.ApprovedAt != null && l.ApprovedAt >= start && l.ApprovedAt < end)
                                         .SumAsync(l => l.Amount),
            RequestsProcessed = await _db.ServiceRequests.CountAsync(r => r.ProcessedAt != null && r.ProcessedAt >= start && r.ProcessedAt < end),
            TotalDeposits = await _db.Accounts.SumAsync(a => a.Balance)
        };
        return View(vm);
    }
}
