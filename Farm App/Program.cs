using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Farm_App.Data;
using Farm_App.Services;
using WebPush;

namespace Farm_App;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddDefaultIdentity<IdentityUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        }).AddEntityFrameworkStores<ApplicationDbContext>();

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
        builder.Services.AddScoped<WebPushService>();
        builder.Services.AddHttpClient("WeatherForecast", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Farm/1.0");
        });
        builder.Services.AddHttpClient("WeatherGeocoding", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(4);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Farm/1.0 (weather location lookup)");
        });
        builder.Services.AddHttpClient("AuctionFeed", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(25);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FarmApp-AuctionCalendar/1.0");
        });
        builder.Services.AddSingleton<AuctionFeedService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<AuctionFeedService>());
        builder.Services.AddHostedService<FarmAlertMonitorService>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();

            if (!db.PushServerSettings.Any(x => x.Id == 1))
            {
                var keys = VapidHelper.GenerateVapidKeys();
                db.PushServerSettings.Add(new Models.PushServerSettings
                {
                    Id = 1,
                    PublicKey = keys.PublicKey,
                    PrivateKey = keys.PrivateKey,
                    Subject = "https://farmapp.runasp.net"
                });
                db.SaveChanges();
            }
        }

        app.MapDefaultEndpoints();
        if (app.Environment.IsDevelopment())
            app.UseMigrationsEndPoint();
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets().AllowAnonymous();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();
        app.MapRazorPages().WithStaticAssets();

        app.Run();
    }
}
