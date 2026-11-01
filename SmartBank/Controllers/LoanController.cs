using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBank.Data;
using SmartBank.Models;
using SmartBank.ViewModels;

namespace SmartBank.Controllers;

[Authorize]
public class LoanController : BaseController
{
    private readonly AppDbContext _db;

    public LoanController(AppDbContext db)
    {
        _db = db;
    }

    // ---------- Application Status: meri saari loan applications ----------

    public async Task<IActionResult> Index()
    {
        var loans = await _db.Loans
            .Where(l => l.CustomerId == CurrentCustomerId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
        return View(loans);
    }

    // ---------- Loan Application (Car / Home / Personal / Business) ----------

    [HttpGet]
    public async Task<IActionResult> Apply(string type = "Personal")
    {
        if (!Bank.LoanTypes.Contains(type)) return NotFound();

        await LoadAccounts(_db);
        ViewBag.Rate = Bank.LoanRate(type);
        return View(new LoanApplyViewModel { LoanType = type, Months = 12 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(LoanApplyViewModel vm)
    {
        if (!Bank.LoanTypes.Contains(vm.LoanType)) return NotFound();

        await LoadAccounts(_db);
        ViewBag.Rate = Bank.LoanRate(vm.LoanType);
        if (!ModelState.IsValid) return View(vm);

        var account = await MyActiveAccount(_db, vm.AccountId);
        if (account == null)
        {
            ModelState.AddModelError("AccountId", "Invalid account.");
            return View(vm);
        }

        // Ek hi type ki ek waqt mein ek pending application
        bool alreadyPending = await _db.Loans.AnyAsync(l =>
            l.CustomerId == CurrentCustomerId && l.LoanType == vm.LoanType && l.Status == "Pending");
        if (alreadyPending)
        {
            ModelState.AddModelError("", "You already have a pending " + vm.LoanType + " loan application.");
            return View(vm);
        }

        decimal amount = Math.Round(vm.Amount!.Value, 2);
        decimal rate = Bank.LoanRate(vm.LoanType);

        _db.Loans.Add(new Loan
        {
            CustomerId = CurrentCustomerId,
            AccountId = account.Id,
            LoanType = vm.LoanType,
            Amount = amount,
            Months = vm.Months,
            InterestRate = rate,
            MonthlyInstallment = Bank.Emi(amount, rate, vm.Months),
            Purpose = (vm.Purpose ?? "").Trim()
        });
        Bank.Notify(_db, CurrentCustomerId, "SMS", $"Your {vm.LoanType} loan application of Rs {amount:N2} has been received.");
        Bank.Log(_db, CurrentCustomerId, "LoanApply", $"{vm.LoanType} Rs {amount:N2} for {vm.Months} months");
        await _db.SaveChangesAsync();

        TempData["Success"] = "Loan application submitted. You can track it here.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Loan Details + Installment Schedule ----------

    public async Task<IActionResult> Details(int id)
    {
        var loan = await _db.Loans
            .Include(l => l.Account)
            .Include(l => l.Installments.OrderBy(i => i.No))
            .FirstOrDefaultAsync(l => l.Id == id && l.CustomerId == CurrentCustomerId);

        if (loan == null) return NotFound();
        return View(loan);
    }

    // ---------- Loan Repayment History: ab tak ki paid installments ----------

    public async Task<IActionResult> History()
    {
        var list = await _db.LoanInstallments
            .Include(i => i.Loan)
            .Where(i => i.IsPaid && i.Loan!.CustomerId == CurrentCustomerId)
            .OrderByDescending(i => i.PaidDate)
            .ToListAsync();
        return View(list);
    }

    // ---------- Installment ada karna ----------

    private Task<LoanInstallment?> MyPayableInstallment(int id)
    {
        return _db.LoanInstallments
            .Include(i => i.Loan)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsPaid
                                   && i.Loan!.CustomerId == CurrentCustomerId
                                   && i.Loan.Status == "Approved");
    }

    [HttpGet]
    public async Task<IActionResult> Pay(int id)
    {
        var inst = await MyPayableInstallment(id);
        if (inst == null) return NotFound();

        await LoadAccounts(_db);
        ViewBag.Installment = inst;
        return View(new LoanPayViewModel { InstallmentId = id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(LoanPayViewModel vm)
    {
        var inst = await MyPayableInstallment(vm.InstallmentId);
        if (inst == null) return NotFound();

        await LoadAccounts(_db);
        ViewBag.Installment = inst;
        if (!ModelState.IsValid) return View(vm);

        // Installments order mein ada hongi
        bool earlierUnpaid = await _db.LoanInstallments.AnyAsync(i =>
            i.LoanId == inst.LoanId && !i.IsPaid && i.No < inst.No);
        if (earlierUnpaid)
        {
            ModelState.AddModelError("", "Please pay the earlier installments first.");
            return View(vm);
        }

        var (customer, source, error) = await PrepareMoney(_db, vm.FromAccountId, vm.Pin);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            return View(vm);
        }

        string? spendError = Bank.Spend(_db, source!, inst.Amount,
            $"{inst.Loan!.LoanType} loan installment #{inst.No}",
            "SmartBank Loans", "LOAN-" + inst.LoanId, out string reference);
        if (spendError != null)
        {
            ModelState.AddModelError("", spendError);
            return View(vm);
        }

        inst.IsPaid = true;
        inst.PaidDate = DateTime.Now;
        inst.PaymentReference = reference;

        // Aakhri installment thi to loan band
        bool moreLeft = await _db.LoanInstallments.AnyAsync(i =>
            i.LoanId == inst.LoanId && !i.IsPaid && i.Id != inst.Id);
        if (!moreLeft) inst.Loan.Status = "Closed";

        Bank.Notify(_db, customer!.Id, "SMS",
            $"Loan installment #{inst.No} of Rs {inst.Amount:N2} paid. Ref {reference}.");
        Bank.Log(_db, customer.Id, "LoanPayment", $"Loan #{inst.LoanId} installment #{inst.No} ref {reference}");

        if (!await Bank.Save(_db))
        {
            ModelState.AddModelError("", "Balance changed while processing. Please try again.");
            return View(vm);
        }

        TempData["Success"] = "Installment #" + inst.No + " paid. Reference " + reference;
        return RedirectToAction(nameof(Details), new { id = inst.LoanId });
    }
}
