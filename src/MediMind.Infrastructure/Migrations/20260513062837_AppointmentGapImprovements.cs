using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentGapImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "requires_payment_before_confirmation",
                table: "healthcare_centers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "waitlist_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preferred_date_from = table.Column<DateOnly>(type: "date", nullable: false),
                    preferred_date_to = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waitlist_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "f_k_waitlist_subscriptions_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_waitlist_subscriptions_healthcare_centers_center_id",
                        column: x => x.center_id,
                        principalTable: "healthcare_centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_waitlist_subscriptions_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_waitlist_subscriptions_center_id",
                table: "waitlist_subscriptions",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "i_x_waitlist_subscriptions_doctor_id_center_id_is_active",
                table: "waitlist_subscriptions",
                columns: new[] { "doctor_id", "center_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "i_x_waitlist_subscriptions_patient_id_doctor_id_center_id",
                table: "waitlist_subscriptions",
                columns: new[] { "patient_id", "doctor_id", "center_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "waitlist_subscriptions");

            migrationBuilder.DropColumn(
                name: "requires_payment_before_confirmation",
                table: "healthcare_centers");
        }
    }
}
