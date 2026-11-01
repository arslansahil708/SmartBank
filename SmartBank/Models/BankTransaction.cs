namespace SmartBank.Models;


public class BankTransaction
{
    public int Id { get; set; }

    public int AccountId { get; set; }
    public Account? Account { get; set; }

    public string Type { get; set; } = ""; 
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }   
    public string Description { get; set; } = "";
    public string Reference { get; set; } = "";     

  
    public string OtherPartyName { get; set; } = "";
    public string OtherPartyAccount { get; set; } = "";

    public DateTime Date { get; set; } = DateTime.Now;
}
