using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartBank.Data;
using SmartBank.Models;
using SmartBank.ViewModels;

namespace SmartBank.Controllers;

[Authorize]
public class PaymentController : BaseController
{
    private readonly AppDbContext _db;

    public PaymentController(AppDbContext db)
    {
        _db = db;
    }

    // Saved beneficiaries ka dropdown (type ke hisaab se)
    private async Task LoadBeneficiaries(string type)
    {
        ViewBag.Beneficiaries = await _db.Beneficiaries
            .Where(b => b.CustomerId == CurrentCustomerId && b.Type == type)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    // =====================================================================
    // 1) SAME BANK TRANSFER
    // =====================================================================

    [HttpGet]
    public async Task<IActionResult> Transfer()
    {
        await LoadAccounts(_db);
        await LoadBeneficiaries("SameBank");
        return View(new TransferViewModel());
    }

    // jQuery ye call karta hai: account number likho to holder ka naam dikhe
    [HttpGet]
    public async Task<IActionResult> LookupAccount(string number)
    {
        var acc = await _db.Accounts
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.AccountNumber == number && a.IsActive);

        if (acc == null) return Json(new { found = false });
        return Json(new { found = true, name = acc.Customer!.FullName });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Transfer(TransferViewModel vm)
    {
        await LoadAccounts(_db);
        await LoadBeneficiaries("SameBank");
        if (!ModelState.IsValid) return View(vm);

        decimal amount = vm.Amount!.Value;
        if (amount != Math.Round(amount, 2))
        {
            ModelState.AddModelError("Amount", "Amount can have at most 2 decimal places.");
            return View(vm);
        }

        // Kisko bhej rahe hain?
        var target = await _db.Accounts
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.AccountNumber == vm.ToAccountNumber && a.IsActive);
        if (target == null)
        {
            ModelState.AddModelError("ToAccountNumber", "Receiver account not found.");
            return View(vm);
        }

        if (target.Id == vm.FromAccountId)
        {
            ModelState.AddModelError("ToAccountNumber", "You cannot transfer to the same account.");
            return View(vm);
        }

        // Customer + source account + PIN
        var (customer, source, error) = await PrepareMoney(_db, vm.FromAccountId, vm.Pin);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            return View(vm);
        }

        string? moveError = Bank.Move(_db, source!, customer!.FullName,
            target, target.Customer!.FullName, amount, vm.Note ?? "", out string reference);
        if (moveError != null)
        {
            ModelState.AddModelError("Amount", moveError);
            return View(vm);
        }

        Bank.Notify(_db, customer.Id, "SMS",
            $"Rs {amount:N2} sent to {target.Customer.FullName}. Ref {reference}. Balance Rs {source!.Balance:N2}.");
        Bank.Notify(_db, target.CustomerId, "SMS",
            $"Rs {amount:N2} received from {customer.FullName}. Ref {reference}.");
        Bank.Log(_db, customer.Id, "Transfer",
            $"Rs {amount:N2} from {source.AccountNumber} to {target.AccountNumber} ref {reference}");

        if (!await Bank.Save(_db))
        {
            ModelState.AddModelError("", "Balance changed while processing. Please try again.");
            return View(vm);
        }

        return RedirectToAction(nameof(Receipt), new { reference });
    }

    // =====================================================================
    // 2) OTHER BANK TRANSFER (demo: paise sirf aapke account se kat'te hain)
    // =====================================================================

    private async Task LoadOtherBankLists()
    {
        await LoadAccounts(_db);
        await LoadBeneficiaries("OtherBank");
        ViewBag.Banks = new SelectList(Bank.OtherBanks);
    }

    [HttpGet]
    public async Task<IActionResult> OtherBank()
    {
        await LoadOtherBankLists();
        return View(new OtherBankTransferViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OtherBank(OtherBankTransferViewModel vm)
    {
        await LoadOtherBankLists();
        if (!ModelState.IsValid) return View(vm);

        decimal amount = vm.Amount!.Value;
        if (amount != Math.Round(amount, 2))
        {
            ModelState.AddModelError("Amount", "Amount can have at most 2 decimal places.");
            return View(vm);
        }

        if (!Bank.OtherBanks.Contains(vm.BankName))
        {
            ModelState.AddModelError("BankName", "Please select a bank.");
            return View(vm);
        }

        var (customer, source, error) = await PrepareMoney(_db, vm.FromAccountId, vm.Pin);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            return View(vm);
        }

        string note = string.IsNullOrWhiteSpace(vm.Note) ? "" : " (" + vm.Note.Trim() + ")";
        string? spendError = Bank.Spend(_db, source!, amount,
            $"Sent to {vm.AccountTitle} at {vm.BankName}{note}",
            vm.AccountTitle, vm.BankName + " " + vm.AccountNumber, out string reference);
        if (spendError != null)
        {
            ModelState.AddModelError("Amount", spendError);
            return View(vm);
        }

        Bank.Notify(_db, customer!.Id, "SMS",
            $"Rs {amount:N2} sent to {vm.AccountTitle} ({vm.BankName}). Ref {reference}. Balance Rs {source!.Balance:N2}.");
        Bank.Log(_db, customer.Id, "OtherBankTransfer",
            $"Rs {amount:N2} from {source.AccountNumber} to {vm.BankName} {vm.AccountNumber} ref {reference}");

        if (!await Bank.Save(_db))
        {
            ModelState.AddModelError("", "Balance changed while processing. Please try again.");
            return View(vm);
        }

        return RedirectToAction(nameof(Receipt), new { reference });
    }

    // =====================================================================
    // 3) RAAST ID TRANSFER
    // Raast ID = mobile number. Demo mein sirf SmartBank ke registered mobile numbers chalte hain.
    // =====================================================================

    [HttpGet]
    public async Task<IActionResult> Raast()
    {
        await LoadAccounts(_db);
        await LoadBeneficiaries("Raast");
        return View(new RaastTransferViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> LookupRaast(string id)
    {
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.Phone == id && x.IsActive && x.Role == "Customer");
        if (c == null) return Json(new { found = false });
        return Json(new { found = true, name = c.FullName });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Raast(RaastTransferViewModel vm)
    {
        await LoadAccounts(_db);
        await LoadBeneficiaries("Raast");
        if (!ModelState.IsValid) return View(vm);

        decimal amount = vm.Amount!.Value;
        if (amount != Math.Round(amount, 2))
        {
            ModelState.AddModelError("Amount", "Amount can have at most 2 decimal places.");
            return View(vm);
        }

        // Raast ID (mobile) se customer, aur uska pehla active account
        var receiver = await _db.Customers
            .FirstOrDefaultAsync(c => c.Phone == vm.RaastId && c.IsActive && c.Role == "Customer");
        Account? target = null;
        if (receiver != null)
        {
            target = await _db.Accounts
                .Where(a => a.CustomerId == receiver.Id && a.IsActive)
                .OrderBy(a => a.Id)
                .FirstOrDefaultAsync();
        }
        if (receiver == null || target == null)
        {
            ModelState.AddModelError("RaastId", "This Raast ID is not registered.");
            return View(vm);
        }

        if (target.Id == vm.FromAccountId)
        {
            ModelState.AddModelError("RaastId", "You cannot transfer to the same account.");
            return View(vm);
        }

        var (customer, source, error) = await PrepareMoney(_db, vm.FromAccountId, vm.Pin);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            return View(vm);
        }

        string? moveError = Bank.Move(_db, source!, customer!.FullName,
            target, receiver.FullName, amount, vm.Note ?? "", out string reference);
        if (moveError != null)
        {
            ModelState.AddModelError("Amount", moveError);
            return View(vm);
        }

        Bank.Notify(_db, customer.Id, "SMS",
            $"Rs {amount:N2} sent to {receiver.FullName} via Raast. Ref {reference}. Balance Rs {source!.Balance:N2}.");
        Bank.Notify(_db, receiver.Id, "SMS",
            $"Rs {amount:N2} received from {customer.FullName} via Raast. Ref {reference}.");
        Bank.Log(_db, customer.Id, "RaastTransfer",
            $"Rs {amount:N2} from {source.AccountNumber} to Raast ID {vm.RaastId} ref {reference}");

        if (!await Bank.Save(_db))
        {
            ModelState.AddModelError("", "Balance changed while processing. Please try again.");
            return View(vm);
        }

        return RedirectToAction(nameof(Receipt), new { reference });
    }

    // =====================================================================
    // 4) MOBILE LOAD (Jazz / Ufone / Zong / Telenor) - demo
    // =====================================================================

    private async Task LoadMobileLists()
    {
        await LoadAccounts(_db);
        ViewBag.Operators = new SelectList(Bank.Operators);
    }

    [HttpGet]
    public async Task<IActionResult> MobileLoad()
    {
        await LoadMobileLists();
        return View(new MobileLoadViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MobileLoad(MobileLoadViewModel vm)
    {
        await LoadMobileLists();
        if (!ModelState.IsValid) return View(vm);

        decimal amount = vm.Amount!.Value;
        if (amount != Math.Round(amount, 2))
        {
            ModelState.AddModelError("Amount", "Amount can have at most 2 decimal places.");
            return View(vm);
        }

        if (!Bank.Operators.Contains(vm.Operator))
        {
            ModelState.AddModelError("Operator", "Please select an operator.");
            return View(vm);
        }

        var (customer, source, error) = await PrepareMoney(_db, vm.FromAccountId, vm.Pin);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            return View(vm);
        }

        string? spendError = Bank.Spend(_db, source!, amount,
            $"Mobile load {vm.Operator} {vm.MobileNumber}",
            vm.Operator, vm.MobileNumber, out string reference);
        if (spendError != null)
        {
            ModelState.AddModelError("Amount", spendError);
            return View(vm);
        }

        _db.BillPayments.Add(new BillPayment
        {
            CustomerId = customer!.Id,
            AccountId = source!.Id,
            Category = "Mobile Load",
            Company = vm.Operator,
            ConsumerNumber = vm.MobileNumber,
            Amount = amount,
            Reference = reference
        });
        Bank.Notify(_db, customer.Id, "SMS",
            $"Rs {amount:N2} {vm.Operator} load sent to {vm.MobileNumber}. Ref {reference}.");
        Bank.Log(_db, customer.Id, "MobileLoad", $"Rs {amount:N2} {vm.Operator} {vm.MobileNumber} ref {reference}");

        if (!await Bank.Save(_db))
        {
            ModelState.AddModelError("", "Balance changed while processing. Please try again.");
            return View(vm);
        }

        return RedirectToAction(nameof(Receipt), new { reference });
    }

    // =====================================================================
    // 5) UTILITY BILLS (bijli, gas, pani, internet) - demo
    // =====================================================================

    [HttpGet]
    public async Task<IActionResult> Bills()
    {
        await LoadAccounts(_db);
        return View(new BillViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Bills(BillViewModel vm)
    {
        await LoadAccounts(_db);
        if (!ModelState.IsValid) return View(vm);

        decimal amount = vm.Amount!.Value;
        if (amount != Math.Round(amount, 2))
        {
            ModelState.AddModelError("Amount", "Amount can have at most 2 decimal places.");
            return View(vm);
        }

        // Company se category nikalte hain (K-Electric -> Electricity)
        string? category = Bank.Companies.FirstOrDefault(x => x.Value.Contains(vm.Company)).Key;
        if (category == null)
        {
            ModelState.AddModelError("Company", "Please select a company.");
            return View(vm);
        }

        var (customer, source, error) = await PrepareMoney(_db, vm.FromAccountId, vm.Pin);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            return View(vm);
        }

        string? spendError = Bank.Spend(_db, source!, amount,
            $"{category} bill {vm.Company} #{vm.ConsumerNumber}",
            vm.Company, vm.ConsumerNumber, out string reference);
        if (spendError != null)
        {
            ModelState.AddModelError("Amount", spendError);
            return View(vm);
        }

        _db.BillPayments.Add(new BillPayment
        {
            CustomerId = customer!.Id,
            AccountId = source!.Id,
            Category = category,
            Company = vm.Company,
            ConsumerNumber = vm.ConsumerNumber,
            Amount = amount,
            Reference = reference
        });
        Bank.Notify(_db, customer.Id, "SMS",
            $"{category} bill of Rs {amount:N2} paid to {vm.Company}. Ref {reference}.");
        Bank.Log(_db, customer.Id, "BillPayment", $"Rs {amount:N2} {vm.Company} #{vm.ConsumerNumber} ref {reference}");

        if (!await Bank.Save(_db))
        {
            ModelState.AddModelError("", "Balance changed while processing. Please try again.");
            return View(vm);
        }

        return RedirectToAction(nameof(Receipt), new { reference });
    }

    // Mobile load + bills ki history
    public async Task<IActionResult> BillHistory()
    {
        var list = await _db.BillPayments
            .Where(b => b.CustomerId == CurrentCustomerId)
            .OrderByDescending(b => b.Date)
            .ToListAsync();
        return View(list);
    }

    // =====================================================================
    // 6) RECEIPT (har tarah ke payment ki)
    // =====================================================================

    public async Task<IActionResult> Receipt(string reference)
    {
        var txn = await _db.Transactions
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t => t.Reference == reference
                                   && t.Type == "Debit"
                                   && t.Account!.CustomerId == CurrentCustomerId);

        if (txn == null) return NotFound();
        return View(txn);
    }

    // =====================================================================
    // 7) BENEFICIARY MANAGEMENT
    // =====================================================================

    public async Task<IActionResult> Beneficiaries()
    {
        ViewBag.Banks = new SelectList(Bank.OtherBanks);
        var list = await _db.Beneficiaries
            .Where(b => b.CustomerId == CurrentCustomerId)
            .OrderBy(b => b.Type).ThenBy(b => b.Name)
            .ToListAsync();
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBeneficiary(string type, string accountNumber, string? bankName, string? name)
    {
        accountNumber = (accountNumber ?? "").Trim();
        var ben = new Beneficiary { CustomerId = CurrentCustomerId, Type = type, AccountNumber = accountNumber };

        if (type == "SameBank")
        {
            // Naam hum database se lete hain (verified), user ka likha hua nahi
            var acc = await _db.Accounts.Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber && a.IsActive);
            if (acc == null) { TempData["Error"] = "SmartBank account not found."; return RedirectToAction(nameof(Beneficiaries)); }
            if (acc.CustomerId == CurrentCustomerId) { TempData["Error"] = "You cannot add your own account."; return RedirectToAction(nameof(Beneficiaries)); }

            ben.Name = acc.Customer!.FullName;
            ben.BankName = "SmartBank";
        }
        else if (type == "Raast")
        {
            if (!Regex.IsMatch(accountNumber, @"^03\d{9}$")) { TempData["Error"] = "Raast ID must be a mobile number like 03001234567."; return RedirectToAction(nameof(Beneficiaries)); }

            var c = await _db.Customers.FirstOrDefaultAsync(x => x.Phone == accountNumber && x.IsActive && x.Role == "Customer");
            if (c == null) { TempData["Error"] = "This Raast ID is not registered."; return RedirectToAction(nameof(Beneficiaries)); }

            ben.Name = c.FullName;
            ben.BankName = "Raast";
        }
        else if (type == "OtherBank")
        {
            if (string.IsNullOrWhiteSpace(bankName) || !Bank.OtherBanks.Contains(bankName)) { TempData["Error"] = "Please select a bank."; return RedirectToAction(nameof(Beneficiaries)); }
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100) { TempData["Error"] = "Please enter the account title."; return RedirectToAction(nameof(Beneficiaries)); }
            if (accountNumber.Length < 5 || accountNumber.Length > 34) { TempData["Error"] = "Account number / IBAN looks wrong."; return RedirectToAction(nameof(Beneficiaries)); }

            ben.Name = name.Trim();
            ben.BankName = bankName;
        }
        else
        {
            return BadRequest();
        }

        bool exists = await _db.Beneficiaries.AnyAsync(b =>
            b.CustomerId == CurrentCustomerId && b.Type == type && b.AccountNumber == accountNumber);
        if (exists) { TempData["Error"] = "This beneficiary is already saved."; return RedirectToAction(nameof(Beneficiaries)); }

        _db.Beneficiaries.Add(ben);
        Bank.Log(_db, CurrentCustomerId, "AddBeneficiary", $"{type} {accountNumber}");
        await _db.SaveChangesAsync();

        TempData["Success"] = "Beneficiary " + ben.Name + " added.";
        return RedirectToAction(nameof(Beneficiaries));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBeneficiary(int id)
    {
        var ben = await _db.Beneficiaries.FirstOrDefaultAsync(b => b.Id == id && b.CustomerId == CurrentCustomerId);
        if (ben == null) return NotFound();

        _db.Beneficiaries.Remove(ben);
        Bank.Log(_db, CurrentCustomerId, "DeleteBeneficiary", $"{ben.Type} {ben.AccountNumber}");
        await _db.SaveChangesAsync();

        TempData["Success"] = "Beneficiary removed.";
        return RedirectToAction(nameof(Beneficiaries));
    }

    // =====================================================================
    // 8) PAYMENT REQUEST aur DIGITAL INVOICE
    // =====================================================================

    public async Task<IActionResult> Requests()
    {
        var vm = new RequestsViewModel
        {
            Received = await _db.PaymentRequests
                .Include(r => r.RequesterCustomer)
                .Where(r => r.PayerCustomerId == CurrentCustomerId)
                .OrderByDescending(r => r.CreatedAt).ToListAsync(),

            Sent = await _db.PaymentRequests
                .Include(r => r.PayerCustomer)
                .Where(r => r.RequesterCustomerId == CurrentCustomerId)
                .OrderByDescending(r => r.CreatedAt).ToListAsync()
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> CreateRequest(string kind = "Request")
    {
        await LoadAccounts(_db);
        return View(new PaymentRequestViewModel { Kind = kind == "Invoice" ? "Invoice" : "Request" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRequest(PaymentRequestViewModel vm)
    {
        await LoadAccounts(_db);
        vm.Kind = vm.Kind == "Invoice" ? "Invoice" : "Request";
        if (!ModelState.IsValid) return View(vm);

        decimal amount = vm.Amount!.Value;
        if (amount != Math.Round(amount, 2))
        {
            ModelState.AddModelError("Amount", "Amount can have at most 2 decimal places.");
            return View(vm);
        }

        var myAccount = await MyActiveAccount(_db, vm.RequesterAccountId);
        if (myAccount == null)
        {
            ModelState.AddModelError("RequesterAccountId", "Invalid account.");
            return View(vm);
        }

        var payerAccount = await _db.Accounts.Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.AccountNumber == vm.PayerAccountNumber && a.IsActive);
        if (payerAccount == null)
        {
            ModelState.AddModelError("PayerAccountNumber", "Payer account not found.");
            return View(vm);
        }
        if (payerAccount.CustomerId == CurrentCustomerId)
        {
            ModelState.AddModelError("PayerAccountNumber", "You cannot send a request to yourself.");
            return View(vm);
        }

        var request = new PaymentRequest
        {
            Kind = vm.Kind,
            InvoiceNumber = vm.Kind == "Invoice"
                ? "INV" + DateTime.Now.ToString("yyMMddHHmmss") + Random.Shared.Next(10, 99)
                : "",
            RequesterCustomerId = CurrentCustomerId,
            RequesterAccountId = myAccount.Id,
            PayerCustomerId = payerAccount.CustomerId,
            Amount = amount,
            Description = vm.Description.Trim(),
            DueDate = vm.DueDate
        };
        _db.PaymentRequests.Add(request);

        string myName = User.Identity?.Name ?? "A customer";
        Bank.Notify(_db, payerAccount.CustomerId, "Alert",
            $"{myName} sent you a {vm.Kind.ToLower()} of Rs {amount:N2}: {request.Description}");
        Bank.Log(_db, CurrentCustomerId, "CreateRequest", $"{vm.Kind} Rs {amount:N2} to {payerAccount.AccountNumber}");
        await _db.SaveChangesAsync();

        TempData["Success"] = vm.Kind + " sent to " + payerAccount.Customer!.FullName + ".";
        return RedirectToAction(nameof(Requests));
    }

    private Task<PaymentRequest?> MyIncomingRequest(int id)
    {
        return _db.PaymentRequests
            .Include(r => r.RequesterCustomer)
            .Include(r => r.RequesterAccount)
            .FirstOrDefaultAsync(r => r.Id == id && r.PayerCustomerId == CurrentCustomerId && r.Status == "Pending");
    }

    [HttpGet]
    public async Task<IActionResult> PayRequest(int id)
    {
        var request = await MyIncomingRequest(id);
        if (request == null) return NotFound();

        await LoadAccounts(_db);
        ViewBag.Request = request;
        return View(new PayRequestViewModel { Id = id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PayRequest(PayRequestViewModel vm)
    {
        var request = await MyIncomingRequest(vm.Id);
        if (request == null) return NotFound();

        await LoadAccounts(_db);
        ViewBag.Request = request;
        if (!ModelState.IsValid) return View(vm);

        var target = request.RequesterAccount!;
        if (!target.IsActive)
        {
            ModelState.AddModelError("", "The receiver's account is not active.");
            return View(vm);
        }

        var (customer, source, error) = await PrepareMoney(_db, vm.FromAccountId, vm.Pin);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            return View(vm);
        }

        string? moveError = Bank.Move(_db, source!, customer!.FullName,
            target, request.RequesterCustomer!.FullName, request.Amount,
            request.Kind == "Invoice" ? "Invoice " + request.InvoiceNumber : request.Description,
            out string reference);
        if (moveError != null)
        {
            ModelState.AddModelError("", moveError);
            return View(vm);
        }

        request.Status = "Paid";
        request.PaidAt = DateTime.Now;

        Bank.Notify(_db, customer.Id, "SMS",
            $"Rs {request.Amount:N2} paid to {request.RequesterCustomer.FullName}. Ref {reference}.");
        Bank.Notify(_db, request.RequesterCustomerId, "SMS",
            $"{customer.FullName} paid your {request.Kind.ToLower()} of Rs {request.Amount:N2}. Ref {reference}.");
        Bank.Log(_db, customer.Id, "PayRequest", $"{request.Kind} #{request.Id} Rs {request.Amount:N2} ref {reference}");

        if (!await Bank.Save(_db))
        {
            ModelState.AddModelError("", "Balance changed while processing. Please try again.");
            return View(vm);
        }

        return RedirectToAction(nameof(Receipt), new { reference });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRequest(int id)
    {
        var request = await MyIncomingRequest(id);
        if (request == null) return NotFound();

        request.Status = "Rejected";
        Bank.Notify(_db, request.RequesterCustomerId, "Alert",
            $"Your {request.Kind.ToLower()} of Rs {request.Amount:N2} was rejected.");
        Bank.Log(_db, CurrentCustomerId, "RejectRequest", $"{request.Kind} #{request.Id}");
        await _db.SaveChangesAsync();

        TempData["Success"] = "Request rejected.";
        return RedirectToAction(nameof(Requests));
    }
}
