using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApp.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PackageTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalCredits = table.Column<int>(type: "integer", nullable: false),
                    PricePerCredit = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageTemplateItems_ClassTypes_ClassTypeId",
                        column: x => x.ClassTypeId,
                        principalTable: "ClassTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageTemplateItems_PackageTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PackageTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageTemplateItems_ClassTypeId",
                table: "PackageTemplateItems",
                column: "ClassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageTemplateItems_TemplateId",
                table: "PackageTemplateItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageTemplates_TenantId",
                table: "PackageTemplates",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageTemplateItems");

            migrationBuilder.DropTable(
                name: "PackageTemplates");
        }
    }
}
