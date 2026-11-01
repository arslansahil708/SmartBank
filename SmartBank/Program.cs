using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SmartBank.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Database connection (appsettings.json se)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("db")
    ));

// Cookie login: login nahi to /Auth/Login par bhej do
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Home/Error";            // role ki ijazat na ho to
        options.ExpireTimeSpan = TimeSpan.FromMinutes(15);   // 15 min inactive to auto logout
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
    });

var app = builder.Build();

// Practice project: app shuru hote hi pending migrations lag jati hain aur admin user ban jata hai
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db, app.Configuration);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Order zaroori hai: pehle Authentication, phir Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
