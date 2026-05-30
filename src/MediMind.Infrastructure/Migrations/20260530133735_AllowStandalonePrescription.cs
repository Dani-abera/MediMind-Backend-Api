using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowStandalonePrescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_prescriptions_appointment_id",
                table: "prescriptions");

            migrationBuilder.AlterColumn<Guid>(
                name: "appointment_id",
                table: "prescriptions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "i_x_prescriptions_appointment_id",
                table: "prescriptions",
                column: "appointment_id",
                unique: true,
                filter: "appointment_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_prescriptions_appointment_id",
                table: "prescriptions");

            migrationBuilder.AlterColumn<Guid>(
                name: "appointment_id",
                table: "prescriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_prescriptions_appointment_id",
                table: "prescriptions",
                column: "appointment_id",
                unique: true);
        }
    }
}
