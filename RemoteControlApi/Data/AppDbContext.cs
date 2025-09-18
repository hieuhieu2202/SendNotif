using Microsoft.EntityFrameworkCore;
using RemoteControlApi.Entities;

namespace RemoteControlApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Application> Applications => Set<Application>();
    public DbSet<AppVersion> AppVersions => Set<AppVersion>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Application>(entity =>
        {
            entity.HasIndex(e => e.AppKey).IsUnique();

            entity.Property(e => e.AppKey)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.DisplayName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime2");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);
        });

        modelBuilder.Entity<AppVersion>(entity =>
        {
            entity.Property(e => e.VersionName)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Platform)
                .HasMaxLength(30);

            entity.Property(e => e.FileUrl)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.FileChecksum)
                .HasMaxLength(128);

            entity.Property(e => e.ReleaseDate)
                .HasColumnType("datetime2");

            entity.HasIndex(e => new { e.ApplicationId, e.VersionName })
                .IsUnique();

            entity.HasOne(e => e.Application)
                .WithMany(a => a.AppVersions)
                .HasForeignKey(e => e.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
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

            entity.HasIndex(e => new { e.ApplicationId, e.IsActive, e.CreatedAt });

            entity.HasOne(e => e.Application)
                .WithMany(a => a.Notifications)
                .HasForeignKey(e => e.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AppVersion)
                .WithMany(v => v.Notifications)
                .HasForeignKey(e => e.AppVersionId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
