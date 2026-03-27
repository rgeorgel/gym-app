using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApp.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SocialFacebook",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialInstagram",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialTikTok",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialWebsite",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialWhatsApp",
                table: "Tenants",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SocialFacebook",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SocialInstagram",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SocialTikTok",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SocialWebsite",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SocialWhatsApp",
                table: "Tenants");
        }
    }
}
