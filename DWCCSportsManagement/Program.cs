using DWCCSportsManagement.Data;
using DWCCSportsManagement.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/Index";      // redirect here if not logged in
        options.AccessDeniedPath = "/Home/Index"; // redirect here if role denied
    });

builder.Services.AddAuthorization();

// Add DbContext
builder.Services.AddDbContext<SportsContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 33))
    )
);

// Add Email Service
builder.Services.AddScoped<IEmailService>(provider => 
    new EmailService(builder.Configuration, provider.GetRequiredService<ILogger<EmailService>>())
);

// ? Register IHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// ? Enable session
builder.Services.AddSession();

var app = builder.Build();

// Database initialization - ensure tables are created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SportsContext>();
    try
    {
        db.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database.");
    }
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication(); // ✅ must come BEFORE UseAuthorization
app.UseSession();        // optional
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
