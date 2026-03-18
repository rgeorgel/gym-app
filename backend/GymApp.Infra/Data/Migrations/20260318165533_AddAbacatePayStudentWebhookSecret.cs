using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApp.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAbacatePayStudentWebhookSecret : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AbacatePayStudentWebhookSecret",
                table: "Tenants",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbacatePayStudentWebhookSecret",
                table: "Tenants");
        }
    }
}
