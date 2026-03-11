using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApp.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultPackageTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPackageTemplateId",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_DefaultPackageTemplateId",
                table: "Tenants",
                column: "DefaultPackageTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_PackageTemplates_DefaultPackageTemplateId",
                table: "Tenants",
                column: "DefaultPackageTemplateId",
                principalTable: "PackageTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_PackageTemplates_DefaultPackageTemplateId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_DefaultPackageTemplateId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DefaultPackageTemplateId",
                table: "Tenants");
        }
    }
}
