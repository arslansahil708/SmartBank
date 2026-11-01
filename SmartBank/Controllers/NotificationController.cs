using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBank.Data;

namespace SmartBank.Controllers;

// SMS / Email / Alerts. Real provider nahi hai, is liye messages app ke andar dikhte hain.
// (OTP ka kaam AuthController ke ForgotPassword / ResetPassword mein hai.)
[Authorize]
public class NotificationController : BaseController
{
    private readonly AppDbContext _db;

    public NotificationController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var list = await _db.Notifications
            .Where(n => n.CustomerId == CurrentCustomerId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();

        // Naye messages pehchanne ke liye ids yaad rakho, phir sab ko read mark kar do
        ViewBag.UnreadIds = list.Where(n => !n.IsRead).Select(n => n.Id).ToHashSet();
        foreach (var n in list) n.IsRead = true;
        await _db.SaveChangesAsync();

        return View(list);
    }
}
