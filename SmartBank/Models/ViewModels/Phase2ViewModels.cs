using System.ComponentModel.DataAnnotations;
using SmartBank.Models;

namespace SmartBank.ViewModels;

// ---------- Payments ----------

public class OtherBankTransferViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select an account.")]
    [Display(Name = "From Account")]
    public int FromAccountId { get; set; }

    [Required]
    [Display(Name = "Bank")]
    public string BankName { get; set; } = "";

    [Required, StringLength(34)]
    [Display(Name = "Account Number / IBAN")]
    public string AccountNumber { get; set; } = "";

    [Required, StringLength(100)]
    [Display(Name = "Account Title (Name)")]
    public string AccountTitle { get; set; } = "";

    [Required, Range(1, 1000000, ErrorMessage = "Amount must be between 1 and 1,000,000.")]
    public decimal? Amount { get; set; }

    [StringLength(100)]
    public string? Note { get; set; }

    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must be exactly 4 digits.")]
    [DataType(DataType.Password)]
    [Display(Name = "Transaction PIN")]
    public string Pin { get; set; } = "";
}

public class RaastTransferViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select an account.")]
    [Display(Name = "From Account")]
    public int FromAccountId { get; set; }

    [Required, RegularExpression(@"^03\d{9}$", ErrorMessage = "Raast ID is a mobile number like 03001234567.")]
    [Display(Name = "Raast ID (Mobile Number)")]
    public string RaastId { get; set; } = "";

    [Required, Range(1, 1000000, ErrorMessage = "Amount must be between 1 and 1,000,000.")]
    public decimal? Amount { get; set; }

    [StringLength(100)]
    public string? Note { get; set; }

    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must be exactly 4 digits.")]
    [DataType(DataType.Password)]
    [Display(Name = "Transaction PIN")]
    public string Pin { get; set; } = "";
}

public class MobileLoadViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select an account.")]
    [Display(Name = "From Account")]
    public int FromAccountId { get; set; }

    [Required]
    public string Operator { get; set; } = "";

    [Required, RegularExpression(@"^03\d{9}$", ErrorMessage = "Mobile must look like 03001234567.")]
    [Display(Name = "Mobile Number")]
    public string MobileNumber { get; set; } = "";

    [Required, Range(50, 5000, ErrorMessage = "Load must be between 50 and 5,000.")]
    public decimal? Amount { get; set; }

    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must be exactly 4 digits.")]
    [DataType(DataType.Password)]
    [Display(Name = "Transaction PIN")]
    public string Pin { get; set; } = "";
}

public class BillViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select an account.")]
    [Display(Name = "From Account")]
    public int FromAccountId { get; set; }

    [Required]
    public string Company { get; set; } = "";

    [Required, StringLength(20)]
    [Display(Name = "Consumer Number")]
    public string ConsumerNumber { get; set; } = "";

    [Required, Range(1, 500000, ErrorMessage = "Amount must be between 1 and 500,000.")]
    [Display(Name = "Bill Amount")]
    public decimal? Amount { get; set; }

    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must be exactly 4 digits.")]
    [DataType(DataType.Password)]
    [Display(Name = "Transaction PIN")]
    public string Pin { get; set; } = "";
}

public class PaymentRequestViewModel
{
    public string Kind { get; set; } = "Request";   // Request ya Invoice

    [Range(1, int.MaxValue, ErrorMessage = "Please select your receiving account.")]
    [Display(Name = "Receive Money In")]
    public int RequesterAccountId { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "Payer Account Number")]
    public string PayerAccountNumber { get; set; } = "";

    [Required, Range(1, 1000000, ErrorMessage = "Amount must be between 1 and 1,000,000.")]
    public decimal? Amount { get; set; }

    [Required, StringLength(150)]
    public string Description { get; set; } = "";

    [DataType(DataType.Date)]
    [Display(Name = "Due Date (optional)")]
    public DateTime? DueDate { get; set; }
}

public class PayRequestViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select an account.")]
    [Display(Name = "Pay From")]
    public int FromAccountId { get; set; }

    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must be exactly 4 digits.")]
    [DataType(DataType.Password)]
    [Display(Name = "Transaction PIN")]
    public string Pin { get; set; } = "";
}

public class RequestsViewModel
{
    public List<PaymentRequest> Received { get; set; } = new();
    public List<PaymentRequest> Sent { get; set; } = new();
}

// ---------- Services ----------

public class ServiceRequestViewModel
{
    public string Type { get; set; } = "";

    [Display(Name = "Account")]
    public int? AccountId { get; set; }

    [StringLength(300)]
    public string? Details { get; set; }
}

public class CertificateViewModel
{
    public Account Account { get; set; } = null!;
    public string HolderName { get; set; } = "";
    public string CertificateNo { get; set; } = "";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int Count { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
}

// ---------- Loans ----------

public class LoanApplyViewModel
{
    public string LoanType { get; set; } = "Personal";

    [Range(1, int.MaxValue, ErrorMessage = "Please select an account.")]
    [Display(Name = "Receive Loan In")]
    public int AccountId { get; set; }

    [Required, Range(10000, 50000000, ErrorMessage = "Amount must be between 10,000 and 50,000,000.")]
    [Display(Name = "Loan Amount")]
    public decimal? Amount { get; set; }

    [Range(6, 240, ErrorMessage = "Months must be between 6 and 240.")]
    [Display(Name = "Number of Months")]
    public int Months { get; set; }

    [StringLength(200)]
    public string? Purpose { get; set; }
}

public class LoanPayViewModel
{
    public int InstallmentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select an account.")]
    [Display(Name = "Pay From")]
    public int FromAccountId { get; set; }

    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must be exactly 4 digits.")]
    [DataType(DataType.Password)]
    [Display(Name = "Transaction PIN")]
    public string Pin { get; set; } = "";
}

// ---------- Profile & Security ----------

public class ProfileEditViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, RegularExpression(@"^03\d{9}$", ErrorMessage = "Mobile must look like 03001234567.")]
    [Display(Name = "Mobile Number")]
    public string Phone { get; set; } = "";
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = "";

    [Required, StringLength(50, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm New Password")]
    public string ConfirmPassword { get; set; } = "";
}

public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}

public class ResetPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "OTP is 6 digits.")]
    [Display(Name = "OTP (6 digits)")]
    public string Otp { get; set; } = "";

    [Required, StringLength(50, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm New Password")]
    public string ConfirmPassword { get; set; } = "";
}

// ---------- Savings & Investments ----------

public class TermDepositViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select an account.")]
    [Display(Name = "From Account")]
    public int AccountId { get; set; }

    [Required, Range(5000, 50000000, ErrorMessage = "Amount must be between 5,000 and 50,000,000.")]
    public decimal? Amount { get; set; }

    public int Months { get; set; } = 12;

    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must be exactly 4 digits.")]
    [DataType(DataType.Password)]
    [Display(Name = "Transaction PIN")]
    public string Pin { get; set; } = "";
}

public class CalculatorViewModel
{
    public decimal? Amount { get; set; }
    public int Months { get; set; } = 12;

    public bool ShowResult { get; set; }
    public decimal Rate { get; set; }
    public decimal Profit { get; set; }
    public decimal MaturityAmount { get; set; }
}

public class InvestmentSummaryViewModel
{
    public decimal AccountsTotal { get; set; }
    public decimal PlansTotal { get; set; }
    public decimal DepositsTotal { get; set; }
    public decimal ExpectedProfit { get; set; }
    public decimal LoanOutstanding { get; set; }
}

// ---------- Admin ----------

public class ReportViewModel
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int NewCustomers { get; set; }
    public int NewAccounts { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public int LoansApproved { get; set; }
    public decimal LoansAmount { get; set; }
    public int RequestsProcessed { get; set; }
    public decimal TotalDeposits { get; set; }   // abhi sab accounts ka kul balance
}
