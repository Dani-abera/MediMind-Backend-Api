using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PatientExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "notification_preference_id",
                table: "patients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_read",
                table: "notification_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "emergency_contacts",
                columns: table => new
                {
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    relationship = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emergency_contacts", x => x.contact_id);
                    table.ForeignKey(
                        name: "f_k_emergency_contacts_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "favorites",
                columns: table => new
                {
                    favorite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    center_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_favorites", x => x.favorite_id);
                    table.CheckConstraint("ck_favorite_target", "(doctor_id IS NOT NULL AND center_id IS NULL) OR (doctor_id IS NULL AND center_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "f_k_favorites_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_record_attachments",
                columns: table => new
                {
                    attachment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    health_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "TIMEZONE('utc', NOW())"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_record_attachments", x => x.attachment_id);
                    table.CheckConstraint("ck_file_size", "file_size_bytes BETWEEN 1 AND 10485760");
                    table.CheckConstraint("ck_file_type", "file_type IN ('application/pdf', 'image/jpeg', 'image/png')");
                    table.ForeignKey(
                        name: "f_k_health_record_attachments_health_records_health_record_id",
                        column: x => x.health_record_id,
                        principalTable: "health_records",
                        principalColumn: "record_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_reminders_push = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    appointment_reminders_sms = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    queue_updates = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    health_prediction_ready = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    medication_reminders = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    promotional_emails = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "patient_medical_histories",
                columns: table => new
                {
                    history_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chronic_conditions = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    allergies = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    current_medications = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    blood_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    smoker = table.Column<bool>(type: "boolean", nullable: true),
                    alcohol_consumption = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    family_history = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_medical_histories", x => x.history_id);
                    table.ForeignKey(
                        name: "f_k_patient_medical_histories_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    center_id = table.Column<Guid>(type: "uuid", nullable: true),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.review_id);
                    table.CheckConstraint("ck_rating", "rating BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_review_target", "(doctor_id IS NOT NULL AND center_id IS NULL) OR (doctor_id IS NULL AND center_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "f_k_reviews_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "appointment_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_reviews_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_patients_notification_preference_id",
                table: "patients",
                column: "notification_preference_id");

            migrationBuilder.CreateIndex(
                name: "i_x_notification_logs_user_id_is_read",
                table: "notification_logs",
                columns: new[] { "user_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "i_x_emergency_contacts_patient_id_is_primary",
                table: "emergency_contacts",
                columns: new[] { "patient_id", "is_primary" });

            migrationBuilder.CreateIndex(
                name: "i_x_favorites_patient_id_center_id",
                table: "favorites",
                columns: new[] { "patient_id", "center_id" },
                unique: true,
                filter: "center_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_favorites_patient_id_doctor_id",
                table: "favorites",
                columns: new[] { "patient_id", "doctor_id" },
                unique: true,
                filter: "doctor_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_health_record_attachments_health_record_id",
                table: "health_record_attachments",
                column: "health_record_id");

            migrationBuilder.CreateIndex(
                name: "i_x_notification_preferences_user_id",
                table: "notification_preferences",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_patient_medical_histories_patient_id",
                table: "patient_medical_histories",
                column: "patient_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_reviews_appointment_id_center_id",
                table: "reviews",
                columns: new[] { "appointment_id", "center_id" },
                unique: true,
                filter: "center_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_reviews_appointment_id_doctor_id",
                table: "reviews",
                columns: new[] { "appointment_id", "doctor_id" },
                unique: true,
                filter: "doctor_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_reviews_center_id",
                table: "reviews",
                column: "center_id",
                filter: "center_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_reviews_doctor_id",
                table: "reviews",
                column: "doctor_id",
                filter: "doctor_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_reviews_patient_id",
                table: "reviews",
                column: "patient_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_patients_notification_preferences_notification_preference_id",
                table: "patients",
                column: "notification_preference_id",
                principalTable: "notification_preferences",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_patients_notification_preferences_notification_preference_id",
                table: "patients");

            migrationBuilder.DropTable(
                name: "emergency_contacts");

            migrationBuilder.DropTable(
                name: "favorites");

            migrationBuilder.DropTable(
                name: "health_record_attachments");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "patient_medical_histories");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropIndex(
                name: "i_x_patients_notification_preference_id",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "i_x_notification_logs_user_id_is_read",
                table: "notification_logs");

            migrationBuilder.DropColumn(
                name: "notification_preference_id",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "is_read",
                table: "notification_logs");
        }
    }
}
