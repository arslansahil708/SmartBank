using SmartBank.Models;

namespace SmartBank.Data;

// Pehli baar app chalne par ek Admin user bana deta hai.
// Email aur password appsettings.json ke "SeedAdmin" section se aate hain.
public static class DbSeeder
{
    public static void Seed(AppDbContext db, IConfiguration config)
    {
        if (db.Customers.Any(c => c.Role == "Admin")) return;

        string? email = config["SeedAdmin:Email"];
        string? password = config["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var admin = new Customer
        {
            FullName = "Bank Admin",
            Cnic = "0000000000000",
            Email = email.Trim().ToLower(),
            Phone = "03000000000",
            Role = "Admin"
        };
        admin.PasswordHash = Bank.HashSecret(admin, password);
        admin.PinHash = Bank.HashSecret(admin, Guid.NewGuid().ToString());   // admin PIN use nahi karta

        db.Customers.Add(admin);
        db.SaveChanges();
    }
}
