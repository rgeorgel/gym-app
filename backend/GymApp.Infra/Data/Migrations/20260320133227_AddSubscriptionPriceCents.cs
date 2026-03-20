using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApp.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPriceCents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubscriptionPriceCents",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: Gym → 4900, BeautySalon → 1900
            migrationBuilder.Sql("""
                UPDATE "Tenants"
                SET "SubscriptionPriceCents" = CASE
                    WHEN "TenantType" = 1 THEN 1900
                    ELSE 4900
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionPriceCents",
                table: "Tenants");
        }
    }
}
