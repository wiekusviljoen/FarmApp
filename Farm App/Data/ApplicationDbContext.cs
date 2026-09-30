using Farm_App.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Farm_App.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Livestock> Livestock => Set<Livestock>();
    public DbSet<RainfallRecord> RainfallRecords => Set<RainfallRecord>();
    public DbSet<LivestockEvent> LivestockEvents => Set<LivestockEvent>();
    public DbSet<OfflineSyncRecord> OfflineSyncRecords => Set<OfflineSyncRecord>();

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
    }
}
