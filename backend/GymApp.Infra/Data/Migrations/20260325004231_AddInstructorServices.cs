using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApp.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInstructorServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstructorServices",
                columns: table => new
                {
                    InstructorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassTypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructorServices", x => new { x.InstructorId, x.ClassTypeId });
                    table.ForeignKey(
                        name: "FK_InstructorServices_ClassTypes_ClassTypeId",
                        column: x => x.ClassTypeId,
                        principalTable: "ClassTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstructorServices_Instructors_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstructorServices_ClassTypeId",
                table: "InstructorServices",
                column: "ClassTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstructorServices");
        }
    }
}
