using Microsoft.EntityFrameworkCore;
using RemoteControlApi.Entities;

namespace RemoteControlApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppVersion> AppVersions => Set<AppVersion>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppVersion>(entity =>
        {
            entity.Property(e => e.VersionName)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.FileUrl)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.FileChecksum)
                .HasMaxLength(128);

            entity.Property(e => e.ReleaseDate)
                .HasColumnType("datetime2");

            entity.HasIndex(e => e.VersionName)
                .IsUnique();
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Message)
                .IsRequired();

            entity.Property(e => e.Link)
                .HasMaxLength(255);

            entity.Property(e => e.FileUrl)
                .HasMaxLength(255);

            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime2");

            entity.HasOne(n => n.AppVersion)
                .WithMany(v => v.Notifications)
                .HasForeignKey(n => n.AppVersionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.IsActive, e.CreatedAt });
        });
    }
}
