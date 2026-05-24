using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEnrolledCentersPhantomColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_healthcare_centers_patients_patient_id",
                table: "healthcare_centers");

            migrationBuilder.DropIndex(
                name: "i_x_healthcare_centers_patient_id",
                table: "healthcare_centers");

            migrationBuilder.DropColumn(
                name: "patient_id",
                table: "healthcare_centers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "patient_id",
                table: "healthcare_centers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_healthcare_centers_patient_id",
                table: "healthcare_centers",
                column: "patient_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_healthcare_centers_patients_patient_id",
                table: "healthcare_centers",
                column: "patient_id",
                principalTable: "patients",
                principalColumn: "user_id");
        }
    }
}
