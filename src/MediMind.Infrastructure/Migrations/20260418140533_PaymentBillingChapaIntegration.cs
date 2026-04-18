using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentBillingChapaIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_health_predictions_patient_id_prediction_date",
                table: "health_predictions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_health_prediction_records",
                table: "health_prediction_records");

            migrationBuilder.AlterColumn<DateTime>(
                name: "payment_date",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "TIMEZONE('utc', NOW())",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "chapa_checkout_url",
                table: "payments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "receipt_url",
                table: "payments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "webhook_received_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "patient_id",
                table: "healthcare_centers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "health_predictions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "TIMEZONE('utc', NOW())",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                table: "health_prediction_records",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<DateTime>(
                name: "booking_date",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "TIMEZONE('utc', NOW())",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<Guid>(
                name: "original_appointment_id",
                table: "appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reminder24h_sent_at",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reminder2h_sent_at",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reschedule_count",
                table: "appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_health_prediction_records",
                table: "health_prediction_records",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "i_x_payments_chapa_transaction_id",
                table: "payments",
                column: "chapa_transaction_id",
                unique: true,
                filter: "chapa_transaction_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_healthcare_centers_patient_id",
                table: "healthcare_centers",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "i_x_health_predictions_patient_id_prediction_date",
                table: "health_predictions",
                columns: new[] { "patient_id", "prediction_date" },
                descending: new[] { false, true });

            migrationBuilder.AddCheckConstraint(
                name: "ck_appointment_date_not_past",
                table: "appointments",
                sql: "appointment_date >= CURRENT_DATE");

            migrationBuilder.AddForeignKey(
                name: "f_k_healthcare_centers_patients_patient_id",
                table: "healthcare_centers",
                column: "patient_id",
                principalTable: "patients",
                principalColumn: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_healthcare_centers_patients_patient_id",
                table: "healthcare_centers");

            migrationBuilder.DropIndex(
                name: "i_x_payments_chapa_transaction_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "i_x_healthcare_centers_patient_id",
                table: "healthcare_centers");

            migrationBuilder.DropIndex(
                name: "i_x_health_predictions_patient_id_prediction_date",
                table: "health_predictions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_health_prediction_records",
                table: "health_prediction_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_appointment_date_not_past",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "chapa_checkout_url",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "receipt_url",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "webhook_received_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "patient_id",
                table: "healthcare_centers");

            migrationBuilder.DropColumn(
                name: "id",
                table: "health_prediction_records");

            migrationBuilder.DropColumn(
                name: "original_appointment_id",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "reminder24h_sent_at",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "reminder2h_sent_at",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "reschedule_count",
                table: "appointments");

            migrationBuilder.AlterColumn<DateTime>(
                name: "payment_date",
                table: "payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "TIMEZONE('utc', NOW())");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "health_predictions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "TIMEZONE('utc', NOW())");

            migrationBuilder.AlterColumn<DateTime>(
                name: "booking_date",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "TIMEZONE('utc', NOW())");

            migrationBuilder.AddPrimaryKey(
                name: "PK_health_prediction_records",
                table: "health_prediction_records",
                columns: new[] { "prediction_id", "record_id" });

            migrationBuilder.CreateIndex(
                name: "i_x_health_predictions_patient_id_prediction_date",
                table: "health_predictions",
                columns: new[] { "patient_id", "prediction_date" });
        }
    }
}
