namespace SmartBank.Models;




public class Otp
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string CodeHash { get; set; } = "";
    public string Purpose { get; set; } = "ResetPassword";
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public int Attempts { get; set; }                  
}
