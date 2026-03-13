using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApp.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPaymentsAllowedDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix rows that got defaultValue: false when the column was added;
            // the intended default is true (super admin allows payments by default).
            migrationBuilder.Sql("UPDATE \"Tenants\" SET \"PaymentsAllowedBySuperAdmin\" = true WHERE \"PaymentsAllowedBySuperAdmin\" = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
