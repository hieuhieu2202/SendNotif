using Microsoft.EntityFrameworkCore;
using RemoteControlApi.Entities;

namespace RemoteControlApi.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        await SeedAppVersionsAsync(context, cancellationToken);
        await SeedNotificationsAsync(context, cancellationToken);
    }

    private static async Task SeedAppVersionsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.AppVersions.AnyAsync(cancellationToken))
        {
            return;
        }

        var versions = new List<AppVersion>
        {
            new()
            {
                VersionName = "1.0.0",
                ReleaseNotes = "Ra mắt ứng dụng",
                FileUrl = "https://example.com/v1.0.0.apk",
                FileChecksum = "a1b2c3",
                ReleaseDate = DateTime.SpecifyKind(new DateTime(2025, 7, 1, 9, 0, 0), DateTimeKind.Utc)
            },
            new()
            {
                VersionName = "1.1.0",
                ReleaseNotes = "Thêm chức năng X, fix bug Y",
                FileUrl = "https://example.com/v1.1.0.apk",
                FileChecksum = "b2c3d4",
                ReleaseDate = DateTime.SpecifyKind(new DateTime(2025, 8, 15, 10, 0, 0), DateTimeKind.Utc)
            },
            new()
            {
                VersionName = "1.2.0",
                ReleaseNotes = "Fix lỗi đăng nhập, UI tối ưu",
                FileUrl = "https://example.com/v1.2.0.apk",
                FileChecksum = "c3d4e5",
                ReleaseDate = DateTime.SpecifyKind(new DateTime(2025, 9, 17, 9, 30, 0), DateTimeKind.Utc)
            }
        };

        await context.AppVersions.AddRangeAsync(versions, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedNotificationsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Notifications.AnyAsync(cancellationToken))
        {
            return;
        }

        var versionsByName = await context.AppVersions
            .AsNoTracking()
            .ToDictionaryAsync(version => version.VersionName, cancellationToken);

        var notifications = new List<Notification>
        {
            new()
            {
                Title = "🎉 Ra mắt ứng dụng",
                Message = "Phiên bản 1.0.0 đã chính thức ra mắt",
                CreatedAt = DateTime.SpecifyKind(new DateTime(2025, 7, 1, 9, 0, 0), DateTimeKind.Utc),
                AppVersionId = versionsByName.TryGetValue("1.0.0", out var version100) ? version100.AppVersionId : null,
                IsActive = false
            },
            new()
            {
                Title = "🚀 Bản cập nhật 1.1.0",
                Message = "Có nhiều cải tiến mới, tải ngay!",
                CreatedAt = DateTime.SpecifyKind(new DateTime(2025, 8, 15, 10, 0, 0), DateTimeKind.Utc),
                AppVersionId = versionsByName.TryGetValue("1.1.0", out var version110) ? version110.AppVersionId : null,
                IsActive = true
            },
            new()
            {
                Title = "⚡ Cập nhật 1.2.0",
                Message = "Fix lỗi đăng nhập + UI dark mode",
                CreatedAt = DateTime.SpecifyKind(new DateTime(2025, 9, 17, 9, 30, 0), DateTimeKind.Utc),
                AppVersionId = versionsByName.TryGetValue("1.2.0", out var version120) ? version120.AppVersionId : null,
                IsActive = true
            },
            new()
            {
                Title = "🔧 Bảo trì hệ thống",
                Message = "Hệ thống sẽ bảo trì 23h ngày 20/09",
                CreatedAt = DateTime.SpecifyKind(new DateTime(2025, 9, 17, 12, 0, 0), DateTimeKind.Utc),
                AppVersionId = null,
                IsActive = true
            }
        };

        await context.Notifications.AddRangeAsync(notifications, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
