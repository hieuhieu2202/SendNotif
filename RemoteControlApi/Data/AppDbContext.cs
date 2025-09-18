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
            entity.HasIndex(a => a.AppKey).IsUnique();
            entity.Property(a => a.AppKey).HasMaxLength(100).IsRequired();
            entity.Property(a => a.DisplayName).HasMaxLength(150).IsRequired();
            entity.Property(a => a.Description).HasMaxLength(500);
            entity.Property(a => a.CreatedAt).HasColumnType("datetime2");
        });

        modelBuilder.Entity<AppVersion>(entity =>
        {
            entity.Property(e => e.VersionName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Platform).HasMaxLength(30);
            entity.Property(e => e.FileUrl).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FileChecksum).HasMaxLength(128);
            entity.Property(e => e.ReleaseDate).HasColumnType("datetime2");

            entity.HasOne(e => e.Application)
                .WithMany(a => a.AppVersions)
                .HasForeignKey(e => e.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ApplicationId, e.VersionName, e.Platform }).IsUnique();
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Message).IsRequired();
            entity.Property(e => e.Link).HasMaxLength(255);
            entity.Property(e => e.FileUrl).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2");

            entity.HasOne(n => n.Application)
                .WithMany(a => a.Notifications)
                .HasForeignKey(n => n.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(n => n.AppVersion)
                .WithMany(v => v.Notifications)
                .HasForeignKey(n => n.AppVersionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(n => new { n.ApplicationId, n.CreatedAt });
        });
    }
}
