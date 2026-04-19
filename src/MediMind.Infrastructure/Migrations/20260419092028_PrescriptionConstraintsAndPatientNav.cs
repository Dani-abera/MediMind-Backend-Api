using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PrescriptionConstraintsAndPatientNav : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_prescriptions_healthcare_centers_center_id",
                table: "prescriptions");

            migrationBuilder.DropForeignKey(
                name: "f_k_prescriptions_patients_patient_id",
                table: "prescriptions");

            migrationBuilder.DropIndex(
                name: "i_x_prescriptions_appointment_id",
                table: "prescriptions");

            migrationBuilder.CreateIndex(
                name: "i_x_prescriptions_appointment_id",
                table: "prescriptions",
                column: "appointment_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "f_k_prescriptions_healthcare_centers_center_id",
                table: "prescriptions",
                column: "center_id",
                principalTable: "healthcare_centers",
                principalColumn: "center_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "f_k_prescriptions_patients_patient_id",
                table: "prescriptions",
                column: "patient_id",
                principalTable: "patients",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_prescriptions_healthcare_centers_center_id",
                table: "prescriptions");

            migrationBuilder.DropForeignKey(
                name: "f_k_prescriptions_patients_patient_id",
                table: "prescriptions");

            migrationBuilder.DropIndex(
                name: "i_x_prescriptions_appointment_id",
                table: "prescriptions");

            migrationBuilder.CreateIndex(
                name: "i_x_prescriptions_appointment_id",
                table: "prescriptions",
                column: "appointment_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_prescriptions_healthcare_centers_center_id",
                table: "prescriptions",
                column: "center_id",
                principalTable: "healthcare_centers",
                principalColumn: "center_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "f_k_prescriptions_patients_patient_id",
                table: "prescriptions",
                column: "patient_id",
                principalTable: "patients",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
