using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionPaymentFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "patient_id",
                table: "payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "appointment_id",
                table: "payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "admin_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "subscription_status",
                table: "healthcare_centers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<int>(
                name: "pending_billing_cycle",
                table: "healthcare_centers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_payment_ref",
                table: "healthcare_centers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_payments_reason_type",
                table: "payments",
                column: "reason_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_payments_reason_type",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "admin_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "pending_billing_cycle",
                table: "healthcare_centers");

            migrationBuilder.DropColumn(
                name: "pending_payment_ref",
                table: "healthcare_centers");

            migrationBuilder.AlterColumn<Guid>(
                name: "patient_id",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "appointment_id",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "subscription_status",
                table: "healthcare_centers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);
        }
    }
}
