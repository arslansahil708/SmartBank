using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBank.Data;
using SmartBank.Models;
using SmartBank.ViewModels;

namespace SmartBank.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    // ---------- REGISTER ----------

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        string email = vm.Email.Trim().ToLower();

        // Email, CNIC ya mobile pehle se registered to nahi?
        bool exists = await _db.Customers.AnyAsync(c =>
            c.Email == email || c.Cnic == vm.Cnic || c.Phone == vm.Phone);
        if (exists)
        {
            ModelState.AddModelError("", "An account with this email, CNIC or mobile number already exists.");
            return View(vm);
        }

        var customer = new Customer
        {
            FullName = vm.FullName.Trim(),
            Cnic = vm.Cnic,
            Email = email,
            Phone = vm.Phone
        };
        customer.PasswordHash = Bank.HashSecret(customer, vm.Password);
        customer.PinHash = Bank.HashSecret(customer, vm.Pin);

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();   // save ke baad customer.Id mil jati hai

        Bank.Log(_db, customer.Id, "Register", "New customer registered");
        Bank.Notify(_db, customer.Id, "Email", "Welcome to SmartBank, " + customer.FullName + "!");
        await _db.SaveChangesAsync();

        TempData["Success"] = "Registration successful. Please login.";
        return RedirectToAction(nameof(Login));
    }

    // ---------- LOGIN ----------

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        string email = vm.Email.Trim().ToLower();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email);
        string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Security: "email galat" ya "password galat" alag alag nahi batate
        if (customer == null)
        {
            Bank.Log(_db, null, "LoginFailed", "Unknown email: " + email + " from " + ip);
            await _db.SaveChangesAsync();
            ModelState.AddModelError("", "Invalid email or password.");
            return View(vm);
        }

        if (!customer.IsActive)
        {
            ModelState.AddModelError("", "Your account is suspended. Please contact the bank.");
            return View(vm);
        }

        // Login attempt limit: lock hai to andar nahi jane denge
        if (customer.LockedUntil != null && customer.LockedUntil > DateTime.Now)
        {
            ModelState.AddModelError("", "Too many failed attempts. Try again after " +
                customer.LockedUntil.Value.ToString("HH:mm") + ".");
            return View(vm);
        }

        if (!Bank.VerifySecret(customer, customer.PasswordHash, vm.Password))
        {
            customer.FailedLoginAttempts++;
            if (customer.FailedLoginAttempts >= 3)
            {
                customer.LockedUntil = DateTime.Now.AddMinutes(15);
                customer.FailedLoginAttempts = 0;
                Bank.Notify(_db, customer.Id, "Alert", "Your login was locked for 15 minutes after 3 wrong passwords.");
            }
            Bank.Log(_db, customer.Id, "LoginFailed", "Wrong password from " + ip);
            await _db.SaveChangesAsync();

            ModelState.AddModelError("", "Invalid email or password.");
            return View(vm);
        }

        // Login theek hai: counters reset
        customer.FailedLoginAttempts = 0;
        customer.LockedUntil = null;
        Bank.Log(_db, customer.Id, "Login", "Login successful from " + ip);
        await _db.SaveChangesAsync();

        // Cookie banate hain: isme user ki pehchan hoti hai
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new Claim(ClaimTypes.Name, customer.FullName),
            new Claim(ClaimTypes.Role, customer.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        if (customer.Role == "Admin")
            return RedirectToAction("Index", "Admin");

        return RedirectToAction("Index", "Account");
    }

    // ---------- LOGOUT ----------

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // ---------- FORGOT PASSWORD (OTP) ----------

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        string email = vm.Email.Trim().ToLower();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email && c.IsActive);

        if (customer != null)
        {
            // Purane OTP band kar do
            var oldOtps = await _db.Otps.Where(o => o.CustomerId == customer.Id && !o.IsUsed).ToListAsync();
            foreach (var old in oldOtps) old.IsUsed = true;

            // 6 digit OTP (RandomNumberGenerator Random se zyada secure hai)
            string code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            _db.Otps.Add(new Otp
            {
                CustomerId = customer.Id,
                CodeHash = Bank.HashSecret(customer, code),
                ExpiresAt = DateTime.Now.AddMinutes(5)
            });
            Bank.Log(_db, customer.Id, "OtpRequested", "Password reset OTP requested");
            await _db.SaveChangesAsync();

            // DEMO: real SMS provider nahi hai, is liye OTP screen par dikhate hain.
            // Real app mein yahan SMS provider ko code bhejte hain aur ye line hata dete hain.
            TempData["DemoOtp"] = code;
        }

        // Email registered ho ya na ho, message same (taake koi email check na kar sake)
        TempData["Success"] = "If this email is registered, an OTP has been sent to the registered mobile number.";
        return RedirectToAction(nameof(ResetPassword), new { email });
    }

    [HttpGet]
    public IActionResult ResetPassword(string? email)
    {
        return View(new ResetPasswordViewModel { Email = email ?? "" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        string email = vm.Email.Trim().ToLower();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email && c.IsActive);

        Otp? otp = null;
        if (customer != null)
        {
            otp = await _db.Otps
                .Where(o => o.CustomerId == customer.Id && !o.IsUsed && o.ExpiresAt > DateTime.Now)
                .OrderByDescending(o => o.Id)
                .FirstOrDefaultAsync();
        }

        if (customer == null || otp == null)
        {
            ModelState.AddModelError("", "Invalid or expired OTP.");
            return View(vm);
        }

        if (!Bank.VerifySecret(customer, otp.CodeHash, vm.Otp))
        {
            otp.Attempts++;
            if (otp.Attempts >= 3) otp.IsUsed = true;   // 3 galat try ke baad OTP khatam
            await _db.SaveChangesAsync();

            ModelState.AddModelError("", "Invalid or expired OTP.");
            return View(vm);
        }

        // OTP sahi: password badlo
        otp.IsUsed = true;
        customer.PasswordHash = Bank.HashSecret(customer, vm.NewPassword);
        customer.FailedLoginAttempts = 0;
        customer.LockedUntil = null;
        Bank.Log(_db, customer.Id, "PasswordReset", "Password changed using OTP");
        Bank.Notify(_db, customer.Id, "Email", "Your SmartBank password was changed. If this was not you, contact the bank.");
        await _db.SaveChangesAsync();

        TempData["Success"] = "Password changed. Please login.";
        return RedirectToAction(nameof(Login));
    }
}
