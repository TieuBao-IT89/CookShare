
using CookShare.Data;
using CookShare.Data.Seeding;
using CookShare.Health;
using CookShare.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

bool seedRecipes = args.Length > 0 && args[0] == "seed-recipes";
var builder = WebApplication.CreateBuilder(seedRecipes ? Array.Empty<string>() : args);

// ASP.NET Core MVC
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<RecipeSeeder>();
builder.Services.AddScoped<IRecipeSeedSource, LocalRecipeSeedSource>();
builder.Services.AddHttpClient<TheMealDbClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.MaxResponseContentBufferSize = 4 * 1024 * 1024;
}).RemoveAllLoggers();
builder.Services.AddScoped<IRecipeSeedSource>(services => services.GetRequiredService<TheMealDbClient>());
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// PostgreSQL - Neon
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ASP.NET Core Identity
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Password
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // User
        options.User.RequireUniqueEmail = true;

        // Lockout
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // Email verification sẽ triển khai riêng
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Cookie Authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.Cookie.Name = "CookShare.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy =
        CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

var app = builder.Build();

if (seedRecipes)
{
    Environment.ExitCode = await RecipeSeedCommand.RunAsync(args[1..], app.Services, Console.Out);
    await app.DisposeAsync();
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Authentication PHẢI đứng trước Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
