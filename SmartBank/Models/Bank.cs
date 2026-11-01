using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartBank.Data;
using SmartBank.Models;

namespace SmartBank.Models;

public static class Bank
{
    private static readonly PasswordHasher<Customer> Hasher = new();

    // Reference number
    public static string NewReference()
    {
        return "SB" + Guid.NewGuid().ToString("N")[..12].ToUpper();
    }

    // Account number
    public static string NewAccountNumber(AppDbContext db)
    {
        string number;

        do
        {
            number = Guid.NewGuid().ToString("N")[..12].ToUpper();
        }
        while (db.Accounts.Any(a => a.AccountNumber == number));

        return number;
    }

    // Password hashing
    public static string HashSecret(Customer customer, string password)
    {
        return Hasher.HashPassword(customer, password);
    }

    // Password verification
    public static bool VerifySecret(
        Customer customer,
        string hashedPassword,
        string password)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword))
            return false;

        var result = Hasher.VerifyHashedPassword(
            customer, hashedPassword, password);

        return result != PasswordVerificationResult.Failed;
    }

    // PIN verification
    public static async Task<string?> CheckPin(
        AppDbContext db,
        Customer customer,
        string pin)
    {
        if (customer.LockedUntil > DateTime.Now)
            return "Your account is temporarily locked.";

        if (string.IsNullOrWhiteSpace(customer.PinHash))
            return "PIN is not configured.";

        var result = Hasher.VerifyHashedPassword(
            customer, customer.PinHash, pin);

        if (result == PasswordVerificationResult.Failed)
        {
            customer.FailedPinAttempts++;

            if (customer.FailedPinAttempts >= 3)
            {
                customer.LockedUntil = DateTime.Now.AddMinutes(15);
                customer.FailedPinAttempts = 0;
            }

            await db.SaveChangesAsync();
            return "Invalid PIN.";
        }

        customer.FailedPinAttempts = 0;
        await db.SaveChangesAsync();

        return null;
    }

    // Spend money
    public static string? Spend(
        AppDbContext db,
        Account source,
        decimal amount,
        string description,
        string otherPartyName,
        string otherPartyAccount,
        out string reference)
    {
        reference = "";

        if (amount <= 0)
            return "Amount must be greater than zero.";

        if (!source.IsActive)
            return "Account is inactive.";

        if (source.Balance < amount)
            return "Insufficient balance.";

        source.Balance -= amount;
        reference = NewReference();

        db.Transactions.Add(new BankTransaction
        {
            AccountId = source.Id,
            Type = "Debit",
            Amount = amount,
            BalanceAfter = source.Balance,
            Description = description,
            Reference = reference,
            OtherPartyName = otherPartyName,
            OtherPartyAccount = otherPartyAccount,
            Date = DateTime.Now
        });

        return null;
    }

    // Transfer money
    public static string? Move(
        AppDbContext db,
        Account source,
        string senderName,
        Account target,
        string receiverName,
        decimal amount,
        string description,
        out string reference)
    {
        reference = "";

        if (amount <= 0)
            return "Amount must be greater than zero.";

        if (!source.IsActive || !target.IsActive)
            return "One of the accounts is inactive.";

        if (source.Id == target.Id)
            return "You cannot transfer to the same account.";

        if (source.Balance < amount)
            return "Insufficient balance.";

        source.Balance -= amount;
        target.Balance += amount;

        reference = NewReference();

        db.Transactions.AddRange(
            new BankTransaction
            {
                AccountId = source.Id,
                Type = "Debit",
                Amount = amount,
                BalanceAfter = source.Balance,
                Description = description,
                Reference = reference,
                OtherPartyName = receiverName,
                OtherPartyAccount = target.AccountNumber,
                Date = DateTime.Now
            },
            new BankTransaction
            {
                AccountId = target.Id,
                Type = "Credit",
                Amount = amount,
                BalanceAfter = target.Balance,
                Description = description,
                Reference = reference,
                OtherPartyName = senderName,
                OtherPartyAccount = source.AccountNumber,
                Date = DateTime.Now
            });

        return null;
    }

    // Audit log
    public static void Log(
        AppDbContext db,
        int? customerId,
        string action,
        string details)
    {
        db.AuditLogs.Add(new AuditLog
        {
            CustomerId = customerId,
            Action = action,
            Details = details,
            CreatedAt = DateTime.Now
        });
    }

    // Notification
    public static void Notify(
        AppDbContext db,
        int customerId,
        string channel,
        string message)
    {
        db.Notifications.Add(new Notification
        {
            CustomerId = customerId,
            Channel = channel,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.Now
        });
    }

    // Bill payment companies
    public static Dictionary<string, List<string>> Companies { get; } = new()
    {
        ["Electricity"] = new() { "K-Electric" },
        ["Gas"] = new() { "Sui Southern Gas" },
        ["Internet"] = new() { "PTCL" },
        ["Water"] = new() { "Water Board" }
    };

    // Mobile operators
    public static List<string> Operators { get; } = new()
    {
        "Jazz", "Ufone", "Zong", "Telenor"
    };

    // Other banks
    public static List<string> OtherBanks { get; } = new()
    {
        "HBL", "UBL", "Meezan Bank", "MCB", "Bank Alfalah"
    };

    // Savings deposit periods
    public static List<int> DepositMonths { get; } = new()
    {
        3, 6, 12, 24, 36
    };

    // Demo annual deposit rate
    public static decimal DepositRate(int months)
    {
        return months switch
        {
            3 => 6m,
            6 => 7m,
            12 => 8m,
            24 => 9m,
            36 => 10m,
            _ => 0m
        };
    }

    // Loan types
    // Loan types
    public static List<string> LoanTypes { get; } = new()
{
    "Personal",
    "Car",
    "Home",
    "Business"
};

    // Annual loan rates
    public static decimal LoanRate(string type)
    {
        return type switch
        {
            "Personal" => 15m,
            "Car" => 12m,
            "Home" => 10m,
            _ => 0m
        };
    }

    // Monthly EMI calculation
    public static decimal Emi(
        decimal amount,
        decimal annualRate,
        int months)
    {
        if (amount <= 0 || months <= 0 || annualRate < 0)
            return 0m;

        decimal monthlyRate = annualRate / 12m / 100m;

        if (monthlyRate == 0)
            return Math.Round(amount / months, 2);

        double r = (double)monthlyRate;
        double n = months;

        double emi = (double)amount * r *
            Math.Pow(1 + r, n) /
            (Math.Pow(1 + r, n) - 1);

        return Math.Round((decimal)emi, 2);
    }

    // Deposit profit calculation
    public static decimal DepositProfit(
        decimal amount,
        decimal annualRate,
        int months)
    {
        if (amount <= 0 || annualRate < 0 || months <= 0)
            return 0m;

        decimal profit = amount * annualRate * months / 1200m;

        return Math.Round(profit, 2);
    }

    // Receive money
    public static void Receive(
        AppDbContext db,
        Account target,
        decimal amount,
        string description,
        string otherPartyName,
        out string reference)
    {
        reference = "";

        if (amount <= 0)
            return;

        if (!target.IsActive)
            return;

        reference = NewReference();
        target.Balance += amount;

        db.Transactions.Add(new BankTransaction
        {
            AccountId = target.Id,
            Type = "Credit",
            Amount = amount,
            BalanceAfter = target.Balance,
            Description = description,
            Reference = reference,
            OtherPartyName = otherPartyName,
            Date = DateTime.Now
        });
    }

    // Save changes
    public static async Task<bool> Save(AppDbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}