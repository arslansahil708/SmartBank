namespace SmartBank.Models;

public class AuditLog
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public string Action { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
