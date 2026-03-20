using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApp.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAvailabilityModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_ScheduleId_Date",
                table: "Sessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScheduleId",
                table: "Sessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ClassTypeId",
                table: "Sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "Sessions",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "ClassTypes",
                type: "integer",
                nullable: true);

            // Backfill denormalized fields on existing gym sessions from their Schedules
            migrationBuilder.Sql("""
                UPDATE "Sessions" s
                SET
                    "TenantId"       = sc."TenantId",
                    "ClassTypeId"    = sc."ClassTypeId",
                    "StartTime"      = sc."StartTime",
                    "DurationMinutes"= sc."DurationMinutes"
                FROM "Schedules" sc
                WHERE s."ScheduleId" = sc."Id";
                """);

            migrationBuilder.CreateTable(
                name: "ProfessionalAvailability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstructorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Weekday = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalAvailability", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessionalAvailability_Instructors_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProfessionalAvailability_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ClassTypeId",
                table: "Sessions",
                column: "ClassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ScheduleId_Date",
                table: "Sessions",
                columns: new[] { "ScheduleId", "Date" },
                unique: true,
                filter: "\"ScheduleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TenantId_Date",
                table: "Sessions",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalAvailability_InstructorId",
                table: "ProfessionalAvailability",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalAvailability_TenantId",
                table: "ProfessionalAvailability",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_ClassTypes_ClassTypeId",
                table: "Sessions",
                column: "ClassTypeId",
                principalTable: "ClassTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Tenants_TenantId",
                table: "Sessions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_ClassTypes_ClassTypeId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Tenants_TenantId",
                table: "Sessions");

            migrationBuilder.DropTable(
                name: "ProfessionalAvailability");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ClassTypeId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ScheduleId_Date",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_TenantId_Date",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ClassTypeId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "ClassTypes");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScheduleId",
                table: "Sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ScheduleId_Date",
                table: "Sessions",
                columns: new[] { "ScheduleId", "Date" },
                unique: true);
        }
    }
}
