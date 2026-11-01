using Microsoft.EntityFrameworkCore;
using SmartBank.Models;

namespace SmartBank.Data;


public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Phase 1
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<BankTransaction> Transactions => Set<BankTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
   

    // Phase 2
    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();
    public DbSet<PaymentRequest> PaymentRequests => Set<PaymentRequest>();
    public DbSet<BillPayment> BillPayments => Set<BillPayment>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<DebitCard> DebitCards => Set<DebitCard>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanInstallment> LoanInstallments => Set<LoanInstallment>();
    public DbSet<SavingsPlan> SavingsPlans => Set<SavingsPlan>();
    public DbSet<TermDeposit> TermDeposits => Set<TermDeposit>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Otp> Otps => Set<Otp>();

    // Paise (decimal) ke liye har jagah decimal(18,2)
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Duplicate na hon
        b.Entity<Customer>().HasIndex(c => c.Email).IsUnique();
        b.Entity<Customer>().HasIndex(c => c.Cnic).IsUnique();
        b.Entity<Account>().HasIndex(a => a.AccountNumber).IsUnique();

        // Bank mein record delete nahi hota. Restrict ka matlab: parent delete karo to error aaye.
        // (Is se SQL Server ka "multiple cascade paths" error bhi nahi aata.)
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
