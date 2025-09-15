using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemoteControlApi.Migrations
{
    public partial class AddDeviceUserInfo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardCode",
                table: "Devices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "Devices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardCode",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "Devices");
        }
    }
}
