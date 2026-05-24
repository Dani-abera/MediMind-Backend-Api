using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentTypeAndAutoVideoConsultation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "appointment_type",
                table: "appointments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "InPerson");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "appointment_type",
                table: "appointments");
        }
    }
}
