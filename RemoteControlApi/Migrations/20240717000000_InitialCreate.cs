using System;
using Microsoft.EntityFrameworkCore.Migrations;
using RemoteControlApi.Data;

#nullable disable

namespace RemoteControlApi.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20240717000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppVersions",
            columns: table => new
            {
                AppVersionId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                VersionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                ReleaseNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                FileUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                FileChecksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                ReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppVersions", x => x.AppVersionId);
            });

        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                NotificationId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Link = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                AppVersionId = table.Column<int>(type: "int", nullable: true),
                FileUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                table.ForeignKey(
                    name: "FK_Notifications_AppVersions_AppVersionId",
                    column: x => x.AppVersionId,
                    principalTable: "AppVersions",
                    principalColumn: "AppVersionId",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.InsertData(
            table: "AppVersions",
            columns: new[] { "AppVersionId", "FileChecksum", "FileUrl", "ReleaseDate", "ReleaseNotes", "VersionName" },
            values: new object[,]
            {
                { 1, "a1b2c3", "https://example.com/v1.0.0.apk", new DateTime(2025, 7, 1, 9, 0, 0, DateTimeKind.Utc), "Ra mắt ứng dụng", "1.0.0" },
                { 2, "b2c3d4", "https://example.com/v1.1.0.apk", new DateTime(2025, 8, 15, 10, 0, 0, DateTimeKind.Utc), "Thêm chức năng X, fix bug Y", "1.1.0" },
                { 3, "c3d4e5", "https://example.com/v1.2.0.apk", new DateTime(2025, 9, 17, 9, 30, 0, DateTimeKind.Utc), "Fix lỗi đăng nhập, UI tối ưu", "1.2.0" }
            });

        migrationBuilder.InsertData(
            table: "Notifications",
            columns: new[] { "NotificationId", "AppVersionId", "CreatedAt", "FileUrl", "IsActive", "Link", "Message", "Title" },
            values: new object[,]
            {
                { 1, 1, new DateTime(2025, 7, 1, 9, 0, 0, DateTimeKind.Utc), null, false, null, "Phiên bản 1.0.0 đã chính thức ra mắt", "🎉 Ra mắt ứng dụng" },
                { 2, 2, new DateTime(2025, 8, 15, 10, 0, 0, DateTimeKind.Utc), null, true, null, "Có nhiều cải tiến mới, tải ngay!", "🚀 Bản cập nhật 1.1.0" },
                { 3, 3, new DateTime(2025, 9, 17, 9, 30, 0, DateTimeKind.Utc), null, true, null, "Fix lỗi đăng nhập + UI dark mode", "⚡ Cập nhật 1.2.0" },
                { 4, null, new DateTime(2025, 9, 17, 12, 0, 0, DateTimeKind.Utc), null, true, null, "Hệ thống sẽ bảo trì 23h ngày 20/09", "🔧 Bảo trì hệ thống" }
            });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_AppVersionId",
            table: "Notifications",
            column: "AppVersionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Notifications");

        migrationBuilder.DropTable(
            name: "AppVersions");
    }
}
