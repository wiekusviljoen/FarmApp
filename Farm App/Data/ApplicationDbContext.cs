using Farm_App.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Farm_App.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<IdentityUser, IdentityRole, string, IdentityUserClaim<string>, IdentityUserRole<string>, IdentityUserLogin<string>, IdentityRoleClaim<string>, IdentityUserToken<string>, IdentityUserPasskey<string>>(options)
{
    public DbSet<Livestock> Livestock => Set<Livestock>();
    public DbSet<FarmCamp> FarmCamps => Set<FarmCamp>();
    public DbSet<RainfallRecord> RainfallRecords => Set<RainfallRecord>();
    public DbSet<LivestockEvent> LivestockEvents => Set<LivestockEvent>();
    public DbSet<OfflineSyncRecord> OfflineSyncRecords => Set<OfflineSyncRecord>();
    public DbSet<AnimalHealthCase> AnimalHealthCases => Set<AnimalHealthCase>();
    public DbSet<FarmPushSubscription> FarmPushSubscriptions => Set<FarmPushSubscription>();
    public DbSet<PushServerSettings> PushServerSettings => Set<PushServerSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Livestock>()
            .Property(x => x.PurchasePrice)
            .HasPrecision(18, 2);
        builder.Entity<OfflineSyncRecord>()
            .HasIndex(x => new { x.OwnerId, x.ClientId })
            .IsUnique();
        builder.Entity<LivestockEvent>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);
        builder.Entity<AnimalHealthCase>()
            .HasIndex(x => new { x.OwnerId, x.CreatedAt });
        builder.Entity<FarmPushSubscription>()
            .HasIndex(x => new { x.OwnerId, x.Endpoint })
            .IsUnique();
        builder.Entity<PushServerSettings>().Property(x => x.AlertStateJson).HasColumnType("TEXT");
    }
}
