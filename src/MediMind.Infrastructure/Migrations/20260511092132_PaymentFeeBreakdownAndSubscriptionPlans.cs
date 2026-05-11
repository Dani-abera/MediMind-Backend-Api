using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentFeeBreakdownAndSubscriptionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "base_amount",
                table: "payments",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "center_id",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "payments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "ETB");

            migrationBuilder.AddColumn<string>(
                name: "reason_type",
                table: "payments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "service_fee",
                table: "payments",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "service_percent",
                table: "payments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "total_amount",
                table: "payments",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_fee",
                table: "payments",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_percent",
                table: "payments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "current_plan",
                table: "healthcare_centers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "subscription_plan_id",
                table: "healthcare_centers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payment_activities",
                columns: table => new
                {
                    activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    chapa_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    payment_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    amount_paid = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    charge_paid = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "ETB"),
                    payment_method_raw = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    gateway_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "TIMEZONE('utc', NOW())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_activities", x => x.activity_id);
                    table.ForeignKey(
                        name: "f_k_payment_activities_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "payment_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    monthly_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    yearly_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    max_doctors = table.Column<int>(type: "integer", nullable: false),
                    max_appointments_per_day = table.Column<int>(type: "integer", nullable: false),
                    features = table.Column<string>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "TIMEZONE('utc', NOW())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.plan_id);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_payments_center_id",
                table: "payments",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "i_x_healthcare_centers_subscription_plan_id",
                table: "healthcare_centers",
                column: "subscription_plan_id");

            migrationBuilder.CreateIndex(
                name: "i_x_payment_activities_created_at",
                table: "payment_activities",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "i_x_payment_activities_payment_id",
                table: "payment_activities",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "i_x_subscription_plans_is_active",
                table: "subscription_plans",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "i_x_subscription_plans_name",
                table: "subscription_plans",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_subscription_plans_tier",
                table: "subscription_plans",
                column: "tier");

            migrationBuilder.AddForeignKey(
                name: "f_k_healthcare_centers_subscription_plans_subscription_plan_id",
                table: "healthcare_centers",
                column: "subscription_plan_id",
                principalTable: "subscription_plans",
                principalColumn: "plan_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_healthcare_centers_subscription_plans_subscription_plan_id",
                table: "healthcare_centers");

            migrationBuilder.DropTable(
                name: "payment_activities");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropIndex(
                name: "i_x_payments_center_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "i_x_healthcare_centers_subscription_plan_id",
                table: "healthcare_centers");

            migrationBuilder.DropColumn(
                name: "base_amount",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "center_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "reason_type",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "service_fee",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "service_percent",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "total_amount",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "vat_fee",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "vat_percent",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "current_plan",
                table: "healthcare_centers");

            migrationBuilder.DropColumn(
                name: "subscription_plan_id",
                table: "healthcare_centers");
        }
    }
}
