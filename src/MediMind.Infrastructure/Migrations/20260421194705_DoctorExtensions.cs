using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DoctorExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "biography",
                table: "doctors",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "appointment_notes",
                columns: table => new
                {
                    note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "TIMEZONE('utc', NOW())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_notes", x => x.note_id);
                    table.ForeignKey(
                        name: "f_k_appointment_notes_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "appointment_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_appointment_notes_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prescription_templates",
                columns: table => new
                {
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    diagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    medications = table.Column<string>(type: "jsonb", nullable: false),
                    lab_tests = table.Column<string>(type: "jsonb", nullable: true),
                    follow_up_instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    use_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "TIMEZONE('utc', NOW())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prescription_templates", x => x.template_id);
                    table.ForeignKey(
                        name: "f_k_prescription_templates_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_appointment_notes_appointment_id",
                table: "appointment_notes",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_appointment_notes_doctor_id",
                table: "appointment_notes",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "i_x_prescription_templates_doctor_id_name",
                table: "prescription_templates",
                columns: new[] { "doctor_id", "name" });

            migrationBuilder.CreateIndex(
                name: "i_x_prescription_templates_doctor_id_use_count",
                table: "prescription_templates",
                columns: new[] { "doctor_id", "use_count" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_notes");

            migrationBuilder.DropTable(
                name: "prescription_templates");

            migrationBuilder.DropColumn(
                name: "biography",
                table: "doctors");
        }
    }
}
