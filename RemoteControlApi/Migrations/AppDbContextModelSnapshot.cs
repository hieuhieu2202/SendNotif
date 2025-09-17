using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RemoteControlApi.Data;

#nullable disable

namespace RemoteControlApi.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.6");

        modelBuilder.Entity("RemoteControlApi.Entities.AppVersion", b =>
        {
            b.Property<int>("AppVersionId")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<string>("FileChecksum")
                .HasMaxLength(128)
                .HasColumnType("TEXT");

            b.Property<string>("FileUrl")
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("TEXT");

            b.Property<DateTime>("ReleaseDate")
                .HasColumnType("TEXT");

            b.Property<string>("ReleaseNotes")
                .HasColumnType("TEXT");

            b.Property<string>("VersionName")
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnType("TEXT");

            b.HasKey("AppVersionId");

            b.ToTable("AppVersions");

            b.HasData(
                new
                {
                    AppVersionId = 1,
                    FileChecksum = "a1b2c3",
                    FileUrl = "https://example.com/v1.0.0.apk",
                    ReleaseDate = new DateTime(2025, 7, 1, 9, 0, 0, DateTimeKind.Utc),
                    ReleaseNotes = "Ra mắt ứng dụng",
                    VersionName = "1.0.0"
                },
                new
                {
                    AppVersionId = 2,
                    FileChecksum = "b2c3d4",
                    FileUrl = "https://example.com/v1.1.0.apk",
                    ReleaseDate = new DateTime(2025, 8, 15, 10, 0, 0, DateTimeKind.Utc),
                    ReleaseNotes = "Thêm chức năng X, fix bug Y",
                    VersionName = "1.1.0"
                },
                new
                {
                    AppVersionId = 3,
                    FileChecksum = "c3d4e5",
                    FileUrl = "https://example.com/v1.2.0.apk",
                    ReleaseDate = new DateTime(2025, 9, 17, 9, 30, 0, DateTimeKind.Utc),
                    ReleaseNotes = "Fix lỗi đăng nhập, UI tối ưu",
                    VersionName = "1.2.0"
                });
        });

        modelBuilder.Entity("RemoteControlApi.Entities.Notification", b =>
        {
            b.Property<int>("NotificationId")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<int?>("AppVersionId")
                .HasColumnType("INTEGER");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("FileUrl")
                .HasMaxLength(255)
                .HasColumnType("TEXT");

            b.Property<bool>("IsActive")
                .HasColumnType("INTEGER");

            b.Property<string>("Link")
                .HasMaxLength(255)
                .HasColumnType("TEXT");

            b.Property<string>("Message")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("Title")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("TEXT");

            b.HasKey("NotificationId");

            b.HasIndex("AppVersionId");

            b.ToTable("Notifications");

            b.HasData(
                new
                {
                    NotificationId = 1,
                    AppVersionId = 1,
                    CreatedAt = new DateTime(2025, 7, 1, 9, 0, 0, DateTimeKind.Utc),
                    FileUrl = (string?)null,
                    IsActive = false,
                    Link = (string?)null,
                    Message = "Phiên bản 1.0.0 đã chính thức ra mắt",
                    Title = "🎉 Ra mắt ứng dụng"
                },
                new
                {
                    NotificationId = 2,
                    AppVersionId = 2,
                    CreatedAt = new DateTime(2025, 8, 15, 10, 0, 0, DateTimeKind.Utc),
                    FileUrl = (string?)null,
                    IsActive = true,
                    Link = (string?)null,
                    Message = "Có nhiều cải tiến mới, tải ngay!",
                    Title = "🚀 Bản cập nhật 1.1.0"
                },
                new
                {
                    NotificationId = 3,
                    AppVersionId = 3,
                    CreatedAt = new DateTime(2025, 9, 17, 9, 30, 0, DateTimeKind.Utc),
                    FileUrl = (string?)null,
                    IsActive = true,
                    Link = (string?)null,
                    Message = "Fix lỗi đăng nhập + UI dark mode",
                    Title = "⚡ Cập nhật 1.2.0"
                },
                new
                {
                    NotificationId = 4,
                    AppVersionId = (int?)null,
                    CreatedAt = new DateTime(2025, 9, 17, 12, 0, 0, DateTimeKind.Utc),
                    FileUrl = (string?)null,
                    IsActive = true,
                    Link = (string?)null,
                    Message = "Hệ thống sẽ bảo trì 23h ngày 20/09",
                    Title = "🔧 Bảo trì hệ thống"
                });
        });

        modelBuilder.Entity("RemoteControlApi.Entities.Notification", b =>
        {
            b.HasOne("RemoteControlApi.Entities.AppVersion", "AppVersion")
                .WithMany("Notifications")
                .HasForeignKey("AppVersionId")
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Notifications_AppVersions_AppVersionId");

            b.Navigation("AppVersion");
        });

        modelBuilder.Entity("RemoteControlApi.Entities.AppVersion", b =>
        {
            b.Navigation("Notifications");
        });
    }
}
