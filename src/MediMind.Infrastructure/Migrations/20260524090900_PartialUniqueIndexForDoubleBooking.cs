using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PartialUniqueIndexForDoubleBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_appointments_no_double_booking",
                table: "appointments");

            migrationBuilder.CreateIndex(
                name: "idx_appointments_no_double_booking",
                table: "appointments",
                columns: new[] { "doctor_id", "center_id", "appointment_date", "appointment_time" },
                unique: true,
                filter: "status <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_appointments_no_double_booking",
                table: "appointments");

            migrationBuilder.CreateIndex(
                name: "idx_appointments_no_double_booking",
                table: "appointments",
                columns: new[] { "doctor_id", "center_id", "appointment_date", "appointment_time" },
                unique: true);
        }
    }
}
