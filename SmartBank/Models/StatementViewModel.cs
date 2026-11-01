namespace SmartBank.Models
{
    public class StatementViewModel
    {
        public Account Account { get; set; } = null!;
        public List<BankTransaction> Transactions { get; set; } = new();
        public int Year { get; set; }
        public int? Month { get; set; }   // null matlab poora saal (annual)
    }
}
