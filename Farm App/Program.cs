using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Farm_App.Data;
using Farm_App.Services;

namespace Farm_App;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // Add services to the container.
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddDefaultIdentity<IdentityUser>(options =>
        {
            // Allow users to sign in immediately after registration; email delivery is not configured yet.
            options.SignIn.RequireConfirmedAccount = false;
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        })
            .AddEntityFrameworkStores<ApplicationDbContext>();
        // Require sign-in by default for every endpoint unless it explicitly allows anonymous access.
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, SmtpEmailSender>();
        builder.Services.AddTransient<IEmailSender<IdentityUser>, SmtpEmailSender>();
        builder.Services.AddControllersWithViews();
        builder.Services.AddMemoryCache();
        builder.Services.AddHttpClient("WeatherForecast", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FarmFlow/1.0");
        });
        builder.Services.AddHttpClient("WeatherGeocoding", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(4);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FarmFlow/1.0 (weather location lookup)");
        });
        builder.Services.AddHttpClient("AuctionFeed", client => { client.Timeout = TimeSpan.FromSeconds(25); client.DefaultRequestHeaders.UserAgent.ParseAdd("FarmApp-AuctionCalendar/1.0"); });
        builder.Services.AddSingleton<AuctionFeedService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<AuctionFeedService>());

        var app = builder.Build();

        // Ensure the SQLite schema is present on hosted deployments as well as local runs.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();
        }

        app.MapDefaultEndpoints();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        // Static CSS, JavaScript, and images must remain public so the public welcome page can render correctly.
        app.MapStaticAssets().AllowAnonymous();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();
        app.MapRazorPages()
           .WithStaticAssets();

        app.Run();
    }
}

