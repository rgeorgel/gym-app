using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApp.Infra.Data.Migrations;

public partial class AddLocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Locations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                IsMain = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Locations", x => x.Id);
                table.ForeignKey(
                    name: "FK_Locations_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Locations_TenantId",
            table: "Locations",
            column: "TenantId");

        migrationBuilder.Sql(@"
            INSERT INTO ""Locations"" (""Id"", ""TenantId"", ""Name"", ""Address"", ""Phone"", ""IsMain"", ""CreatedAt"")
            SELECT 
                gen_random_uuid(),
                t.""Id"",
                'Matriz',
                NULL,
                NULL,
                true,
                NOW()
            FROM ""Tenants"" t
            WHERE NOT EXISTS (
                SELECT 1 FROM ""Locations"" l WHERE l.""TenantId"" = t.""Id""
            );
        ");

        migrationBuilder.AddColumn<Guid>(
            name: "LocationId",
            table: "Sessions",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.AddColumn<Guid>(
            name: "LocationId",
            table: "Schedules",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.AddColumn<Guid>(
            name: "PrimaryLocationId",
            table: "Instructors",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_LocationId",
            table: "Sessions",
            column: "LocationId");

        migrationBuilder.CreateIndex(
            name: "IX_Schedules_LocationId",
            table: "Schedules",
            column: "LocationId");

        migrationBuilder.CreateIndex(
            name: "IX_Instructors_PrimaryLocationId",
            table: "Instructors",
            column: "PrimaryLocationId");

        migrationBuilder.Sql(@"
            UPDATE ""Sessions"" s
            SET ""LocationId"" = (
                SELECT l.""Id"" FROM ""Locations"" l 
                WHERE l.""TenantId"" = s.""TenantId"" AND l.""IsMain"" = true
                LIMIT 1
            )
            WHERE ""LocationId"" = '00000000-0000-0000-0000-000000000000';
        ");

        migrationBuilder.Sql(@"
            UPDATE ""Schedules"" sc
            SET ""LocationId"" = (
                SELECT l.""Id"" FROM ""Locations"" l 
                WHERE l.""TenantId"" = sc.""TenantId"" AND l.""IsMain"" = true
                LIMIT 1
            )
            WHERE ""LocationId"" = '00000000-0000-0000-0000-000000000000';
        ");

        migrationBuilder.AddForeignKey(
            name: "FK_Instructors_Locations_PrimaryLocationId",
            table: "Instructors",
            column: "PrimaryLocationId",
            principalTable: "Locations",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Sessions_Locations_LocationId",
            table: "Sessions",
            column: "LocationId",
            principalTable: "Locations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Schedules_Locations_LocationId",
            table: "Schedules",
            column: "LocationId",
            principalTable: "Locations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Sessions_Locations_LocationId",
            table: "Sessions");

        migrationBuilder.DropForeignKey(
            name: "FK_Schedules_Locations_LocationId",
            table: "Schedules");

        migrationBuilder.DropForeignKey(
            name: "FK_Instructors_Locations_PrimaryLocationId",
            table: "Instructors");

        migrationBuilder.DropTable(
            name: "Locations");

        migrationBuilder.DropColumn(
            name: "LocationId",
            table: "Sessions");

        migrationBuilder.DropColumn(
            name: "LocationId",
            table: "Schedules");

        migrationBuilder.DropColumn(
            name: "PrimaryLocationId",
            table: "Instructors");
    }
}
