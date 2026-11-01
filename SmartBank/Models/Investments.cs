namespace SmartBank.Models;


public class SavingsPlan
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string Name { get; set; } = "";
    public decimal TargetAmount { get; set; }
    public decimal SavedAmount { get; set; }
    public bool IsClosed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}


public class TermDeposit
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int AccountId { get; set; }
    public Account? Account { get; set; }

    public decimal Amount { get; set; }
    public int Months { get; set; }
    public decimal ProfitRate { get; set; }          
    public decimal MaturityAmount { get; set; }      
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime MaturityDate { get; set; }

    public string Status { get; set; } = "Active";   
}
