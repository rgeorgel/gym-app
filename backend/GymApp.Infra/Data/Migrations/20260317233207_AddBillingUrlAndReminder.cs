using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApp.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingUrlAndReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AbacatePayBillingUrl",
                table: "Tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbacatePayBillingUrl",
                table: "Tenants");
        }
    }
}
