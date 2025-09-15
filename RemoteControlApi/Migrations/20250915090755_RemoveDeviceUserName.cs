using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemoteControlApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeviceUserName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserName",
                table: "Devices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "Devices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
