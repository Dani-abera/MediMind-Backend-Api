using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SuperAdminModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "healthcare_centers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "healthcare_centers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "healthcare_centers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "license_verification_notes",
                table: "doctors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "license_verified",
                table: "doctors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "subscription_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_status = table.Column<string>(type: "text", nullable: false),
                    new_status = table.Column<string>(type: "text", nullable: false),
                    plan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_histories", x => x.id);
                    table.ForeignKey(
                        name: "f_k_subscription_histories_healthcare_centers_center_id",
                        column: x => x.center_id,
                        principalTable: "healthcare_centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "super_admins",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_super_admins", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_super_admins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_subscription_histories_center_id",
                table: "subscription_histories",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "i_x_subscription_histories_changed_at",
                table: "subscription_histories",
                column: "changed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscription_histories");

            migrationBuilder.DropTable(
                name: "super_admins");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "healthcare_centers");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "healthcare_centers");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "healthcare_centers");

            migrationBuilder.DropColumn(
                name: "license_verification_notes",
                table: "doctors");

            migrationBuilder.DropColumn(
                name: "license_verified",
                table: "doctors");
        }
    }
}
