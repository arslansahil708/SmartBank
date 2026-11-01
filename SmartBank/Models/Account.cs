using System.ComponentModel.DataAnnotations;

namespace SmartBank.Models
{
    public class Account
    {

        public int Id { get; set; }
        public string AccountNumber { get; set; } = "";
        public string AccountType { get; set; } = "Savings";  
        public decimal Balance { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime OpenedAt { get; set; } = DateTime.Now;

        
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

       
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
