using Microsoft.AspNetCore.Mvc;

namespace SmartBank.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        // Agar user login hai to uske dashboard par bhejo
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Index", "Admin");

            return RedirectToAction("Index", "Account");
        }

        return View();
    }

    public IActionResult Error() => View();
}
