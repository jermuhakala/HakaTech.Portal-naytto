using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HakaTech.Portal.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteDesktopConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemoteDesktopConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Protocol = table.Column<int>(type: "int", nullable: false),
                    Hostname = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EncryptedPassword = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IgnoreCert = table.Column<bool>(type: "bit", nullable: false),
                    Security = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteDesktopConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteDesktopConnections_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteDesktopConnections_CustomerId",
                table: "RemoteDesktopConnections",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemoteDesktopConnections");
        }
    }
}
