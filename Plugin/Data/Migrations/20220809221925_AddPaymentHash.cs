using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    public partial class AddPaymentHash : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentHash",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PaymentHash",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions",
                column: "PaymentHash");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PaymentRequest",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions",
                column: "PaymentRequest");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_PaymentHash",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_PaymentRequest",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PaymentHash",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions");
        }
    }
}
