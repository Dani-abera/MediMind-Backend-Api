using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_health_records_patient_id_record_date",
                table: "health_records");

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "record_time",
                table: "health_records",
                type: "time without time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIME",
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "health_records",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "TIMEZONE('utc', NOW())",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateIndex(
                name: "i_x_health_records_patient_id_record_date",
                table: "health_records",
                columns: new[] { "patient_id", "record_date" },
                descending: new[] { false, true });

            migrationBuilder.AddCheckConstraint(
                name: "ck_height",
                table: "health_records",
                sql: "height BETWEEN 50 AND 250 OR height IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_oxygen_saturation",
                table: "health_records",
                sql: "oxygen_saturation BETWEEN 70 AND 100 OR oxygen_saturation IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_recorded_by",
                table: "health_records",
                sql: "recorded_by IN ('patient', 'doctor')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_respiratory_rate",
                table: "health_records",
                sql: "respiratory_rate BETWEEN 8 AND 60 OR respiratory_rate IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_weight",
                table: "health_records",
                sql: "weight BETWEEN 20 AND 300 OR weight IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_health_records_patient_id_record_date",
                table: "health_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_height",
                table: "health_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_oxygen_saturation",
                table: "health_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_recorded_by",
                table: "health_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_respiratory_rate",
                table: "health_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_weight",
                table: "health_records");

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "record_time",
                table: "health_records",
                type: "time without time zone",
                nullable: false,
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone",
                oldDefaultValueSql: "CURRENT_TIME");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "health_records",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "TIMEZONE('utc', NOW())");

            migrationBuilder.CreateIndex(
                name: "i_x_health_records_patient_id_record_date",
                table: "health_records",
                columns: new[] { "patient_id", "record_date" });
        }
    }
}
