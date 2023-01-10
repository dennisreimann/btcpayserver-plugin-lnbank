using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    public partial class UpdateAccessKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessKeys_UserId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys");

            migrationBuilder.DropIndex(
                name: "IX_AccessKeys_WalletId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Admin");

            migrationBuilder.CreateIndex(
                name: "IX_AccessKeys_WalletId_UserId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys",
                columns: new[] { "WalletId", "UserId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessKeys_WalletId_UserId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys",
                type: "text",
                nullable: false,
                defaultValue: "Admin",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_AccessKeys_UserId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessKeys_WalletId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "AccessKeys",
                column: "WalletId");
        }
    }
}
