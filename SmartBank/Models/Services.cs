namespace SmartBank.Models;


public class ServiceRequest
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? AccountId { get; set; }                
    public Account? Account { get; set; }

    public string Type { get; set; } = "";
    public string Details { get; set; } = "";
    public string Status { get; set; } = "Pending";    
    public string AdminRemarks { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ProcessedAt { get; set; }
}

public class DebitCard
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int AccountId { get; set; }
    public Account? Account { get; set; }

    
    public string MaskedNumber { get; set; } = "";
    public string Expiry { get; set; } = "";           
    public string Status { get; set; } = "Active";     
}
