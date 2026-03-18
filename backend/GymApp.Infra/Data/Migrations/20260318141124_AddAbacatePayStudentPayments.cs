using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApp.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAbacatePayStudentPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_EfiTxId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "EfiTxId",
                table: "Payments");

            migrationBuilder.RenameColumn(
                name: "PixQrCodeBase64",
                table: "Payments",
                newName: "AbacatePayBillingUrl");

            migrationBuilder.RenameColumn(
                name: "PixCopyPaste",
                table: "Payments",
                newName: "AbacatePayBillingId");

            migrationBuilder.AddColumn<string>(
                name: "AbacatePayCustomerId",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AbacatePayStudentApiKey",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_AbacatePayBillingId",
                table: "Payments",
                column: "AbacatePayBillingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_AbacatePayBillingId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "AbacatePayCustomerId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AbacatePayStudentApiKey",
                table: "Tenants");

            migrationBuilder.RenameColumn(
                name: "AbacatePayBillingUrl",
                table: "Payments",
                newName: "PixQrCodeBase64");

            migrationBuilder.RenameColumn(
                name: "AbacatePayBillingId",
                table: "Payments",
                newName: "PixCopyPaste");

            migrationBuilder.AddColumn<string>(
                name: "EfiTxId",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_EfiTxId",
                table: "Payments",
                column: "EfiTxId");
        }
    }
}
