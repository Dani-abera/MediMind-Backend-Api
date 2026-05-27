using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RelaxAppointmentDatePastCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Order matters: the OLD constraint (appointment_date >= today)
            // fires on every UPDATE — even one that doesn't change the date —
            // so we can't clean stale rows until it's gone.
            migrationBuilder.DropCheckConstraint(
                name: "ck_appointment_date_not_past",
                table: "appointments");

            // Stale Pending rows with past dates would violate the new
            // partial constraint at validation time (PostgreSQL re-checks
            // existing rows when a CHECK constraint is added). They can
            // never legitimately transition to Confirmed anyway — mark them
            // NoShow so the lifecycle endpoints still work on them.
            migrationBuilder.Sql(
                "UPDATE appointments " +
                "SET status = 'NoShow', updated_at = TIMEZONE('utc', NOW()) " +
                "WHERE status = 'Pending' AND appointment_date < CURRENT_DATE;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_appointment_date_not_past",
                table: "appointments",
                sql: "status <> 'Pending' OR appointment_date >= CURRENT_DATE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_appointment_date_not_past",
                table: "appointments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_appointment_date_not_past",
                table: "appointments",
                sql: "appointment_date >= CURRENT_DATE");
        }
    }
}
