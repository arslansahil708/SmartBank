namespace SmartBank.Models;


public class Beneficiary
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string Type { get; set; } = "SameBank";    
    public string Name { get; set; } = "";
    public string AccountNumber { get; set; } = "";   
    public string BankName { get; set; } = "SmartBank";
}


public class PaymentRequest
{
    public int Id { get; set; }
    public string Kind { get; set; } = "Request";      
    public string InvoiceNumber { get; set; } = "";    
    public int RequesterCustomerId { get; set; }
    public Customer? RequesterCustomer { get; set; }
    public int RequesterAccountId { get; set; }        
    public Account? RequesterAccount { get; set; }

    // Paise dene wala
    public int PayerCustomerId { get; set; }
    public Customer? PayerCustomer { get; set; }

    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "Pending";    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? PaidAt { get; set; }
}


public class BillPayment
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int AccountId { get; set; }
    public Account? Account { get; set; }

    public string Category { get; set; } = "";         
    public string Company { get; set; } = "";          
    public string ConsumerNumber { get; set; } = "";   
    public decimal Amount { get; set; }
    public string Reference { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
}
