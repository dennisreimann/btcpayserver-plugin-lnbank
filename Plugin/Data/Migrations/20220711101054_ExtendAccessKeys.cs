using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    public partial class ExtendAccessKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Level",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys",
                type: "text",
                nullable: false,
                defaultValue: "Admin");

            migrationBuilder.CreateIndex(
                name: "IX_AccessKeys_UserId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessKeys_UserId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys");

            migrationBuilder.DropColumn(
                name: "Level",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys");
        }
    }
}
