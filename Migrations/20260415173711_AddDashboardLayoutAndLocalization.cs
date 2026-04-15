using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HakaTech.Portal.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardLayoutAndLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DashboardLayout",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DashboardLayout",
                table: "AspNetUsers");
        }
    }
}
