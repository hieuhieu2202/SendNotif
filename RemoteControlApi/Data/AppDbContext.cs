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
            entity.Property(e => e.VersionName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FileUrl).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FileChecksum).HasMaxLength(128);
            entity.Property(e => e.ReleaseDate).HasColumnType("TEXT");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Link).HasMaxLength(255);
            entity.Property(e => e.FileUrl).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnType("TEXT");

            entity.HasOne(n => n.AppVersion)
                .WithMany(a => a.Notifications)
                .HasForeignKey(n => n.AppVersionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AppVersion>().HasData(
            new AppVersion
            {
                AppVersionId = 1,
                VersionName = "1.0.0",
                ReleaseNotes = "Ra mắt ứng dụng",
                FileUrl = "https://example.com/v1.0.0.apk",
                FileChecksum = "a1b2c3",
                ReleaseDate = DateTime.SpecifyKind(new DateTime(2025, 7, 1, 9, 0, 0), DateTimeKind.Utc)
            },
            new AppVersion
            {
                AppVersionId = 2,
                VersionName = "1.1.0",
                ReleaseNotes = "Thêm chức năng X, fix bug Y",
                FileUrl = "https://example.com/v1.1.0.apk",
                FileChecksum = "b2c3d4",
                ReleaseDate = DateTime.SpecifyKind(new DateTime(2025, 8, 15, 10, 0, 0), DateTimeKind.Utc)
            },
            new AppVersion
            {
                AppVersionId = 3,
                VersionName = "1.2.0",
                ReleaseNotes = "Fix lỗi đăng nhập, UI tối ưu",
                FileUrl = "https://example.com/v1.2.0.apk",
                FileChecksum = "c3d4e5",
                ReleaseDate = DateTime.SpecifyKind(new DateTime(2025, 9, 17, 9, 30, 0), DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<Notification>().HasData(
            new Notification
            {
                NotificationId = 1,
                Title = "🎉 Ra mắt ứng dụng",
                Message = "Phiên bản 1.0.0 đã chính thức ra mắt",
                CreatedAt = DateTime.SpecifyKind(new DateTime(2025, 7, 1, 9, 0, 0), DateTimeKind.Utc),
                AppVersionId = 1,
                FileUrl = null,
                IsActive = false
            },
            new Notification
            {
                NotificationId = 2,
                Title = "🚀 Bản cập nhật 1.1.0",
                Message = "Có nhiều cải tiến mới, tải ngay!",
                CreatedAt = DateTime.SpecifyKind(new DateTime(2025, 8, 15, 10, 0, 0), DateTimeKind.Utc),
                AppVersionId = 2,
                FileUrl = null,
                IsActive = true
            },
            new Notification
            {
                NotificationId = 3,
                Title = "⚡ Cập nhật 1.2.0",
                Message = "Fix lỗi đăng nhập + UI dark mode",
                CreatedAt = DateTime.SpecifyKind(new DateTime(2025, 9, 17, 9, 30, 0), DateTimeKind.Utc),
                AppVersionId = 3,
                FileUrl = null,
                IsActive = true
            },
            new Notification
            {
                NotificationId = 4,
                Title = "🔧 Bảo trì hệ thống",
                Message = "Hệ thống sẽ bảo trì 23h ngày 20/09",
                CreatedAt = DateTime.SpecifyKind(new DateTime(2025, 9, 17, 12, 0, 0), DateTimeKind.Utc),
                AppVersionId = null,
                FileUrl = null,
                IsActive = true
            }
        );
    }
}
