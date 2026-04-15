using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HakaTech.Portal.Migrations
{
    /// <inheritdoc />
    public partial class AddGuacamoleConnectionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuacamoleConnectionId",
                table: "RemoteDesktopConnections",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuacamoleConnectionId",
                table: "RemoteDesktopConnections");
        }
    }
}
