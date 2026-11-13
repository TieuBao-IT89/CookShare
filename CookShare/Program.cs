using System.Security.Claims;
using System.Threading.RateLimiting;
using CookShare.Data;
using CookShare.Data.Seeding;
using CookShare.Health;
using CookShare.Identity;
using CookShare.Models.Identity;
using CookShare.Options;
using CookShare.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

bool seedRecipes = args.Length > 0 && args[0] == "seed-recipes";
bool moderatorCommand = args.Length > 0 && args[0] == "moderator";
bool cleanupSocialMedia = args.Length > 0 && args[0] == "cleanup-social-media";
var builder = WebApplication.CreateBuilder(seedRecipes || moderatorCommand || cleanupSocialMedia ? Array.Empty<string>() : args);

// ASP.NET Core MVC
builder.Services.AddControllersWithViews();
builder.Services.Configure<RecipeImageStorageOptions>(
    builder.Configuration.GetSection(RecipeImageStorageOptions.SectionName));
builder.Services.AddSingleton<IRecipeImageStorage, LocalRecipeImageStorage>();
builder.Services.Configure<SocialImageStorageOptions>(builder.Configuration.GetSection(SocialImageStorageOptions.SectionName));
builder.Services.AddSingleton<ISocialImageStorage, LocalSocialImageStorage>();
builder.Services.AddScoped<RecipeCollectionService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<CookedPostService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    foreach (string policy in new[] { "cooked-upload", "reports" })
    {
        options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }
});
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

        // Người dùng vẫn đăng nhập được trước khi xác minh email.
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddErrorDescriber<VietnameseIdentityErrorDescriber>();

builder.Services
    .Configure<DataProtectionTokenProviderOptions>(options =>
    {
        options.TokenLifespan = TimeSpan.FromHours(1);
    });

builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddScoped<
    IAccountEmailSender, SmtpAccountEmailSender>();
builder.Services.AddScoped<AccountDeletionService>();

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

if (moderatorCommand || cleanupSocialMedia)
{
    await using var scope = app.Services.CreateAsyncScope();
    if (cleanupSocialMedia)
    {
        await scope.ServiceProvider.GetRequiredService<CookedPostService>().CleanOrphanedImagesAsync();
        Console.WriteLine("Completed the bounded orphan-media cleanup batch.");
    }
    else if (args.Length != 3 || args[1] is not ("grant" or "revoke"))
    {
        Console.Error.WriteLine("Usage: moderator grant|revoke <existing-username>");
        Environment.ExitCode = 1;
    }
    else
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await users.FindByNameAsync(args[2]);
        if (user == null)
        {
            Console.Error.WriteLine("Account was not found.");
            Environment.ExitCode = 1;
        }
        else
        {
            var roleResult = await roles.RoleExistsAsync("Moderator")
                ? IdentityResult.Success : await roles.CreateAsync(new IdentityRole("Moderator"));
            var result = !roleResult.Succeeded ? roleResult : args[1] == "grant"
                ? await users.AddToRoleAsync(user, "Moderator") : await users.RemoveFromRoleAsync(user, "Moderator");
            Environment.ExitCode = result.Succeeded ? 0 : 1;
            Console.WriteLine(result.Succeeded ? "Moderator membership updated. Sign in again to refresh role claims." : "Moderator membership update failed.");
        }
    }
    await app.DisposeAsync();
    return;
}

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
app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
