namespace SmartBank.Models;

public class Loan
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int AccountId { get; set; }                 
    public Account? Account { get; set; }

    public string LoanType { get; set; } = "";         
    public decimal Amount { get; set; }
    public int Months { get; set; }
    public decimal InterestRate { get; set; }          
    public decimal MonthlyInstallment { get; set; }
    public string Purpose { get; set; } = "";

    public string Status { get; set; } = "Pending";    
    public string AdminRemarks { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ApprovedAt { get; set; }

    public List<LoanInstallment> Installments { get; set; } = new();
}

public class LoanInstallment
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public Loan? Loan { get; set; }

    public int No { get; set; }                        
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public string PaymentReference { get; set; } = "";
}
