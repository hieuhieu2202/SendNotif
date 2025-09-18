using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
            name: "Applications",
            columns: table => new
            {
                ApplicationId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                AppKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Applications", x => x.ApplicationId);
            });

        migrationBuilder.CreateTable(
            name: "AppVersions",
            columns: table => new
            {
                AppVersionId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                VersionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Platform = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                ReleaseNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                FileUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                FileChecksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                ReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                ApplicationId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppVersions", x => x.AppVersionId);
                table.ForeignKey(
                    name: "FK_AppVersions_Applications_ApplicationId",
                    column: x => x.ApplicationId,
                    principalTable: "Applications",
                    principalColumn: "ApplicationId",
                    onDelete: ReferentialAction.Cascade);
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
                ApplicationId = table.Column<int>(type: "int", nullable: false),
                AppVersionId = table.Column<int>(type: "int", nullable: true),
                FileUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                table.ForeignKey(
                    name: "FK_Notifications_Applications_ApplicationId",
                    column: x => x.ApplicationId,
                    principalTable: "Applications",
                    principalColumn: "ApplicationId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Notifications_AppVersions_AppVersionId",
                    column: x => x.AppVersionId,
                    principalTable: "AppVersions",
                    principalColumn: "AppVersionId",
                    onDelete: ReferentialAction.NoAction);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Applications_AppKey",
            table: "Applications",
            column: "AppKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AppVersions_ApplicationId",
            table: "AppVersions",
            column: "ApplicationId");

        migrationBuilder.CreateIndex(
            name: "IX_AppVersions_ApplicationId_VersionName_Platform",
            table: "AppVersions",
            columns: new[] { "ApplicationId", "VersionName", "Platform" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_AppVersionId",
            table: "Notifications",
            column: "AppVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_ApplicationId_CreatedAt",
            table: "Notifications",
            columns: new[] { "ApplicationId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Notifications");

        migrationBuilder.DropTable(
            name: "AppVersions");

        migrationBuilder.DropTable(
            name: "Applications");
    }
}
