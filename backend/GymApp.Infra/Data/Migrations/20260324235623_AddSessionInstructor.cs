using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApp.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionInstructor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InstructorId",
                table: "Sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_InstructorId",
                table: "Sessions",
                column: "InstructorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Instructors_InstructorId",
                table: "Sessions",
                column: "InstructorId",
                principalTable: "Instructors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Instructors_InstructorId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_InstructorId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "InstructorId",
                table: "Sessions");
        }
    }
}
