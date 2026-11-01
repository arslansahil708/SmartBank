using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBank.Data;
using SmartBank.Models;
using SmartBank.ViewModels;

namespace SmartBank.Controllers;

[Authorize]
public class InvestmentController : BaseController
{
    private readonly AppDbContext _db;

    public InvestmentController(AppDbContext db)
    {
        _db = db;
    }

    // ---------- Investment Summary ----------

    public async Task<IActionResult> Index()
    {
        int me = CurrentCustomerId;

        var vm = new InvestmentSummaryViewModel
        {
            AccountsTotal = await _db.Accounts.Where(a => a.CustomerId == me).SumAsync(a => a.Balance),
            PlansTotal = await _db.SavingsPlans.Where(p => p.CustomerId == me && !p.IsClosed).SumAsync(p => p.SavedAmount),
            DepositsTotal = await _db.TermDeposits.Where(d => d.CustomerId == me && d.Status == "Active").SumAsync(d => d.Amount),
            ExpectedProfit = await _db.TermDeposits.Where(d => d.CustomerId == me && d.Status == "Active")
                                                   .SumAsync(d => d.MaturityAmount - d.Amount),
            LoanOutstanding = await _db.LoanInstallments
                .Where(i => !i.IsPaid && i.Loan!.CustomerId == me && i.Loan.Status == "Approved")
                .SumAsync(i => i.Amount)
        };
        return View(vm);
    }

    // ---------- Profit / Return Calculator ----------

    [HttpGet]
    public IActionResult Calculator(decimal? amount, int months = 12)
    {
        var vm = new CalculatorViewModel { Amount = amount, Months = months };

        if (amount != null && amount > 0 && Bank.DepositMonths.Contains(months))
        {
            vm.ShowResult = true;
            vm.Rate = Bank.DepositRate(months);
            vm.Profit = Bank.DepositProfit(
                amount.Value,
                Bank.DepositRate(months),
                months
            );
        }
        return View(vm);
    }

    // =====================================================================
    // SAVINGS PLANS
    // =====================================================================

    public async Task<IActionResult> Plans()
    {
        await LoadAccounts(_db);
        var plans = await _db.SavingsPlans
            .Where(p => p.CustomerId == CurrentCustomerId)
            .OrderBy(p => p.IsClosed).ThenByDescending(p => p.CreatedAt)
            .ToListAsync();
        return View(plans);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePlan(string name, decimal targetAmount)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0 || name.Length > 50)
        {
            TempData["Error"] = "Plan name is required (max 50 characters).";
            return RedirectToAction(nameof(Plans));
        }
        if (targetAmount < 1 || targetAmount > 100000000)
        {
            TempData["Error"] = "Target amount must be between 1 and 100,000,000.";
            return RedirectToAction(nameof(Plans));
        }

        _db.SavingsPlans.Add(new SavingsPlan
        {
            CustomerId = CurrentCustomerId,
            Name = name,
            TargetAmount = Math.Round(targetAmount, 2)
        });
        Bank.Log(_db, CurrentCustomerId, "CreatePlan", name);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Savings plan \"" + name + "\" created.";
        return RedirectToAction(nameof(Plans));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DepositToPlan(int id, int fromAccountId, decimal amount, string pin)
    {
        if (amount < 1 || amount != Math.Round(amount, 2))
        {
            TempData["Error"] = "Enter a valid amount (minimum 1, max 2 decimals).";
            return RedirectToAction(nameof(Plans));
        }

        var plan = await _db.SavingsPlans.FirstOrDefaultAsync(p => p.Id == id && p.CustomerId == CurrentCustomerId && !p.IsClosed);
        if (plan == null) return NotFound();

        var (customer, source, error) = await PrepareMoney(_db, fromAccountId, pin ?? "");
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Plans));
        }

        string? spendError = Bank.Spend(_db, source!, amount,
            "Deposit to savings plan: " + plan.Name, "SmartBank Savings", "PLAN-" + plan.Id, out string reference);
        if (spendError != null)
        {
            TempData["Error"] = spendError;
            return RedirectToAction(nameof(Plans));
        }

        plan.SavedAmount += amount;
        Bank.Notify(_db, customer!.Id, "SMS", $"Rs {amount:N2} added to savings plan {plan.Name}. Ref {reference}.");
        Bank.Log(_db, customer.Id, "PlanDeposit", $"Plan #{plan.Id} Rs {amount:N2} ref {reference}");

        if (!await Bank.Save(_db))
        {
            TempData["Error"] = "Balance changed while processing. Please try again.";
            return RedirectToAction(nameof(Plans));
        }

        TempData["Success"] = "Rs " + amount.ToString("N2") + " added to " + plan.Name + ". Reference " + reference;
        return RedirectToAction(nameof(Plans));
    }

    // Plan band: jama paisa pehle (sab se purane) active account mein wapas
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ClosePlan(int id)
    {
        var plan = await _db.SavingsPlans.FirstOrDefaultAsync(p => p.Id == id && p.CustomerId == CurrentCustomerId && !p.IsClosed);
        if (plan == null) return NotFound();

        if (plan.SavedAmount > 0)
        {
            var account = await _db.Accounts
                .Where(a => a.CustomerId == CurrentCustomerId && a.IsActive)
                .OrderBy(a => a.Id)
                .FirstOrDefaultAsync();
            if (account == null)
            {
                TempData["Error"] = "You need an active account to receive the money.";
                return RedirectToAction(nameof(Plans));
            }

            Bank.Receive(
    _db,
    account,
    plan.SavedAmount,
    "Savings plan closed: " + plan.Name,
    "SmartBank Savings",
    out var savingsReference
);
            Bank.Notify(_db, CurrentCustomerId, "SMS", $"Savings plan {plan.Name} closed. Rs {plan.SavedAmount:N2} returned to {account.AccountNumber}.");
        }

        plan.IsClosed = true;
        Bank.Log(_db, CurrentCustomerId, "ClosePlan", $"Plan #{plan.Id}");

        if (!await Bank.Save(_db))
        {
            TempData["Error"] = "Balance changed while processing. Please try again.";
            return RedirectToAction(nameof(Plans));
        }

        TempData["Success"] = "Plan closed and money returned to your account.";
        return RedirectToAction(nameof(Plans));
    }

    // =====================================================================
    // TERM DEPOSITS
    // =====================================================================

    public async Task<IActionResult> Deposits()
    {
        var list = await _db.TermDeposits
            .Include(d => d.Account)
            .Where(d => d.CustomerId == CurrentCustomerId)
            .OrderByDescending(d => d.StartDate)
            .ToListAsync();
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> NewDeposit()
    {
        await LoadAccounts(_db);
        return View(new TermDepositViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewDeposit(TermDepositViewModel vm)
    {
        await LoadAccounts(_db);
        if (!ModelState.IsValid) return View(vm);

        decimal amount = vm.Amount!.Value;
        if (amount != Math.Round(amount, 2))
        {
            ModelState.AddModelError("Amount", "Amount can have at most 2 decimal places.");
            return View(vm);
        }
        if (!Bank.DepositMonths.Contains(vm.Months))
        {
            ModelState.AddModelError("Months", "Please select a valid period.");
            return View(vm);
        }

        var (customer, source, error) = await PrepareMoney(_db, vm.AccountId, vm.Pin);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            return View(vm);
        }

        string? spendError = Bank.Spend(_db, source!, amount,
            $"Term deposit ({vm.Months} months)", "SmartBank Investments", "TD", out string reference);
        if (spendError != null)
        {
            ModelState.AddModelError("Amount", spendError);
            return View(vm);
        }

        var deposit = new TermDeposit
        {
            CustomerId = customer!.Id,
            AccountId = source!.Id,
            Amount = amount,
            Months = vm.Months,
            ProfitRate = Bank.DepositRate(vm.Months),
            MaturityAmount = amount + Bank.DepositProfit(
    amount,
    Bank.DepositRate(vm.Months),
    vm.Months
),
            StartDate = DateTime.Today,
            MaturityDate = DateTime.Today.AddMonths(vm.Months)
        };
        _db.TermDeposits.Add(deposit);

        Bank.Notify(_db, customer.Id, "SMS",
            $"Term deposit of Rs {amount:N2} for {vm.Months} months created. Maturity Rs {deposit.MaturityAmount:N2} on {deposit.MaturityDate:dd MMM yyyy}.");
        Bank.Log(_db, customer.Id, "TermDeposit", $"Rs {amount:N2} for {vm.Months} months ref {reference}");

        if (!await Bank.Save(_db))
        {
            ModelState.AddModelError("", "Balance changed while processing. Please try again.");
            return View(vm);
        }

        TempData["Success"] = "Term deposit created. Reference " + reference;
        return RedirectToAction(nameof(Deposits));
    }

    // Maturity ke baad poori raqam + profit. Pehle band karo to sirf asal raqam (profit nahi).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Redeem(int id)
    {
        var deposit = await _db.TermDeposits
            .Include(d => d.Account)
            .FirstOrDefaultAsync(d => d.Id == id && d.CustomerId == CurrentCustomerId && d.Status == "Active");
        if (deposit == null) return NotFound();

        bool matured = DateTime.Today >= deposit.MaturityDate;
        decimal payout = matured ? deposit.MaturityAmount : deposit.Amount;

        Bank.Receive(
    _db,
    deposit.Account!,
    payout,
    "Deposit payout",
    "SmartBank Deposit",
    out var depositReference
);

        Bank.Notify(_db, CurrentCustomerId, "SMS", $"Rs {payout:N2} credited to {deposit.Account!.AccountNumber} from your term deposit.");
        Bank.Log(_db, CurrentCustomerId, "RedeemDeposit", $"Deposit #{deposit.Id} Rs {payout:N2} matured={matured}");

        if (!await Bank.Save(_db))
        {
            TempData["Error"] = "Balance changed while processing. Please try again.";
            return RedirectToAction(nameof(Deposits));
        }

        TempData["Success"] = "Rs " + payout.ToString("N2") + " credited to your account" + (matured ? "." : " (early close, no profit).");
        return RedirectToAction(nameof(Deposits));
    }

    // ---------- Savings Certificate (printable) ----------

    public async Task<IActionResult> Certificate(int id)
    {
        var deposit = await _db.TermDeposits
            .Include(d => d.Customer)
            .Include(d => d.Account)
            .FirstOrDefaultAsync(d => d.Id == id && d.CustomerId == CurrentCustomerId);
        if (deposit == null) return NotFound();

        return View(deposit);
    }
}
