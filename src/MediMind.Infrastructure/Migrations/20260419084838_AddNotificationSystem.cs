using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "medication_reminders",
                columns: table => new
                {
                    reminder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    medication_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    dosage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reminder_times = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medication_reminders", x => x.reminder_id);
                    table.ForeignKey(
                        name: "f_k_medication_reminders_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_logs",
                columns: table => new
                {
                    log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    notification_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    external_reference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_logs", x => x.log_id);
                });

            migrationBuilder.CreateTable(
                name: "user_device_tokens",
                columns: table => new
                {
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fcm_token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    device_platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    device_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    registered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_device_tokens", x => x.token_id);
                    table.ForeignKey(
                        name: "f_k_user_device_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_medication_reminders_patient_id_is_active",
                table: "medication_reminders",
                columns: new[] { "patient_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "i_x_notification_logs_user_id_sent_at",
                table: "notification_logs",
                columns: new[] { "user_id", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "i_x_user_device_tokens_user_id_fcm_token",
                table: "user_device_tokens",
                columns: new[] { "user_id", "fcm_token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_user_device_tokens_user_id_is_active",
                table: "user_device_tokens",
                columns: new[] { "user_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medication_reminders");

            migrationBuilder.DropTable(
                name: "notification_logs");

            migrationBuilder.DropTable(
                name: "user_device_tokens");
        }
    }
}
