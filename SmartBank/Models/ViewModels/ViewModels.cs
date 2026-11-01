using System.ComponentModel.DataAnnotations;
using SmartBank.Models;
using SmartBank.ViewModels;

namespace SmartBank.ViewModels;



public class RegisterViewModel
{
    [Required, StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = "";

    [Required, RegularExpression(@"^\d{13}$", ErrorMessage = "CNIC must be 13 digits (no dashes).")]
    [Display(Name = "CNIC (13 digits)")]
    public string Cnic { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, RegularExpression(@"^03\d{9}$", ErrorMessage = "Mobile must look like 03001234567.")]
    [Display(Name = "Mobile Number")]
    public string Phone { get; set; } = "";

    [Required, StringLength(50, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = "";

    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must be exactly 4 digits.")]
    [DataType(DataType.Password)]
    [Display(Name = "Transaction PIN (4 digits)")]
    public string Pin { get; set; } = "";
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public class TransferViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select an account.")]
    [Display(Name = "From Account")]
    public int FromAccountId { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "To Account Number")]
    public string ToAccountNumber { get; set; } = "";

    // decimal? isliye taake form shuru mein khali dikhe (0 na dikhe)
    [Required, Range(1, 1000000, ErrorMessage = "Amount must be between 1 and 1,000,000.")]
    public decimal? Amount { get; set; }

    [StringLength(100)]
    public string? Note { get; set; }

    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must be exactly 4 digits.")]
    [DataType(DataType.Password)]
    [Display(Name = "Transaction PIN")]
    public string Pin { get; set; } = "";
}




