namespace SmartBank.Models;


public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Cnic { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";

    
    public string PasswordHash { get; set; } = "";
    public string PinHash { get; set; } = "";

    public string Role { get; set; } = "Customer";   
    public bool IsActive { get; set; } = true;

    
    public int FailedLoginAttempts { get; set; }
    public int FailedPinAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    
    public List<Account> Accounts { get; set; } = new();
}
