using ActivityTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ActivityTracker.Data;

public class TrackerDbContext : DbContext
{
    public TrackerDbContext(
        DbContextOptions<TrackerDbContext> options)
        : base(options)
    {
    }


    public DbSet<ActivityLog> ActivityLogs
        => Set<ActivityLog>();


    protected override void OnModelCreating(
        ModelBuilder builder)
    {
        base.OnModelCreating(builder);


        builder.Entity<ActivityLog>(entity =>
        {
            entity.ToTable("ActivityLogs");


            entity.HasIndex(x => x.IsSynced);


            entity.HasIndex(x => x.StartTime);


            entity.HasIndex(x => x.MachineId);


            entity.Property(x => x.CreatedAt)
                  .HasDefaultValueSql("CURRENT_TIMESTAMP");


            entity.Property(x => x.IsSynced)
                  .HasDefaultValue(false);
        });
    }
}