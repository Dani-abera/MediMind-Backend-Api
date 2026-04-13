using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "healthcare_centers",
                columns: table => new
                {
                    center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    center_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    center_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    license_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    working_hours = table.Column<string>(type: "jsonb", nullable: false),
                    services_offered = table.Column<string>(type: "text", nullable: false),
                    specializations = table.Column<string>(type: "text", nullable: false),
                    subscription_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subscription_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    subscription_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    slot_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    advance_booking_days = table.Column<int>(type: "integer", nullable: false),
                    cancellation_hours = table.Column<int>(type: "integer", nullable: false),
                    auto_approve_appointments = table.Column<bool>(type: "boolean", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,8)", precision: 10, scale: 8, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(11,8)", precision: 11, scale: 8, nullable: true),
                    profile_image_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_healthcare_centers", x => x.center_id);
                    table.CheckConstraint("ck_advance_booking", "advance_booking_days BETWEEN 1 AND 90");
                    table.CheckConstraint("ck_slot_duration", "slot_duration_minutes IN (15, 30, 45, 60)");
                });

            migrationBuilder.CreateTable(
                name: "otp_verifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    expiration_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_verifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    profile_image_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    last_login = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    otp_code = table.Column<string>(type: "text", nullable: true),
                    otp_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    otp_attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "doctors",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    badge_number = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    specialization = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    license_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    years_of_experience = table.Column<int>(type: "integer", nullable: false),
                    qualifications = table.Column<string>(type: "text", nullable: true),
                    languages_spoken = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctors", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_doctors_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "healthcare_center_admins",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    department = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_healthcare_center_admins", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_healthcare_center_admins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_healthcare_center_admins_healthcare_centers_center_id",
                        column: x => x.center_id,
                        principalTable: "healthcare_centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "patients",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    blood_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    allergies = table.Column<string>(type: "text", nullable: true),
                    emergency_contact_name = table.Column<string>(type: "text", nullable: true),
                    emergency_contact_phone = table.Column<string>(type: "text", nullable: true),
                    medical_history = table.Column<string>(type: "text", nullable: true),
                    chronic_conditions = table.Column<string>(type: "text", nullable: false),
                    current_medications = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patients", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_patients_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "doctor_healthcare_centers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consultation_fee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    joined_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctor_healthcare_centers", x => x.id);
                    table.CheckConstraint("ck_consultation_fee", "consultation_fee > 0");
                    table.ForeignKey(
                        name: "f_k_doctor_healthcare_centers_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_doctor_healthcare_centers_healthcare_centers_center_id",
                        column: x => x.center_id,
                        principalTable: "healthcare_centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "doctor_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    working_days = table.Column<string>(type: "text", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    slot_duration = table.Column<int>(type: "integer", nullable: false),
                    break_start = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    break_end = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctor_schedules", x => x.id);
                    table.CheckConstraint("ck_break_times", "break_end > break_start OR break_start IS NULL");
                    table.CheckConstraint("ck_schedule_times", "end_time > start_time");
                    table.ForeignKey(
                        name: "f_k_doctor_schedules_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_doctor_schedules_healthcare_centers_center_id",
                        column: x => x.center_id,
                        principalTable: "healthcare_centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    appointment_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason_for_visit = table.Column<string>(type: "text", nullable: false),
                    symptoms = table.Column<string>(type: "text", nullable: true),
                    booking_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    check_in_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    check_out_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    approved_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointments", x => x.appointment_id);
                    table.CheckConstraint("ck_duration_minutes", "duration_minutes IN (15, 30, 45, 60)");
                    table.ForeignKey(
                        name: "f_k_appointments__healthcare_center_admins_approved_by_admin_id",
                        column: x => x.approved_by_admin_id,
                        principalTable: "healthcare_center_admins",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "f_k_appointments_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_appointments_healthcare_centers_center_id",
                        column: x => x.center_id,
                        principalTable: "healthcare_centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_appointments_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "health_predictions",
                columns: table => new
                {
                    prediction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prediction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    prediction_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    diabetes_risk = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    diabetes_category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hypertension_risk = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    hypertension_category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cvd_risk = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    cvd_category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    model_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    contributing_factors = table.Column<string>(type: "jsonb", nullable: false),
                    recommendations = table.Column<string>(type: "text", nullable: false),
                    data_points_used = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_predictions", x => x.prediction_id);
                    table.CheckConstraint("ck_data_points", "data_points_used > 0");
                    table.ForeignKey(
                        name: "f_k_health_predictions_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_records",
                columns: table => new
                {
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_date = table.Column<DateOnly>(type: "date", nullable: false),
                    record_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    systolic_bp = table.Column<int>(type: "integer", nullable: true),
                    diastolic_bp = table.Column<int>(type: "integer", nullable: true),
                    glucose_level = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    weight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    height = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    temperature = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    heart_rate = table.Column<int>(type: "integer", nullable: true),
                    oxygen_saturation = table.Column<int>(type: "integer", nullable: true),
                    respiratory_rate = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    recorded_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "patient"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_records", x => x.record_id);
                    table.CheckConstraint("ck_bp_relation", "systolic_bp > diastolic_bp OR systolic_bp IS NULL OR diastolic_bp IS NULL");
                    table.CheckConstraint("ck_diastolic_bp", "diastolic_bp BETWEEN 40 AND 150 OR diastolic_bp IS NULL");
                    table.CheckConstraint("ck_glucose", "glucose_level BETWEEN 30 AND 600 OR glucose_level IS NULL");
                    table.CheckConstraint("ck_heart_rate", "heart_rate BETWEEN 30 AND 250 OR heart_rate IS NULL");
                    table.CheckConstraint("ck_systolic_bp", "systolic_bp BETWEEN 70 AND 250 OR systolic_bp IS NULL");
                    table.CheckConstraint("ck_temperature", "temperature BETWEEN 35 AND 43 OR temperature IS NULL");
                    table.ForeignKey(
                        name: "f_k_health_records_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    payment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    chapa_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.payment_id);
                    table.CheckConstraint("ck_payment_amount", "amount > 0");
                    table.ForeignKey(
                        name: "f_k_payments_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "appointment_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_payments_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prescriptions",
                columns: table => new
                {
                    prescription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    diagnosis = table.Column<string>(type: "text", nullable: false),
                    medications = table.Column<string>(type: "jsonb", nullable: false),
                    lab_tests = table.Column<string>(type: "text", nullable: false),
                    follow_up_instructions = table.Column<string>(type: "text", nullable: true),
                    special_instructions = table.Column<string>(type: "text", nullable: true),
                    prescription_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    qr_code = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prescriptions", x => x.prescription_id);
                    table.CheckConstraint("ck_expiry_date", "expiry_date > issue_date OR expiry_date IS NULL");
                    table.ForeignKey(
                        name: "f_k_prescriptions_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "appointment_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_prescriptions_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_prescriptions_healthcare_centers_center_id",
                        column: x => x.center_id,
                        principalTable: "healthcare_centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_prescriptions_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "queue",
                columns: table => new
                {
                    queue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    queue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    queue_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estimated_wait_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    called_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consultation_start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consultation_end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_queue", x => x.queue_id);
                    table.CheckConstraint("ck_estimated_wait", "estimated_wait_time_minutes >= 0");
                    table.CheckConstraint("ck_queue_position", "position > 0");
                    table.ForeignKey(
                        name: "f_k_queue_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "appointment_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_queue_healthcare_centers_center_id",
                        column: x => x.center_id,
                        principalTable: "healthcare_centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "video_consultations",
                columns: table => new
                {
                    consultation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    video_quality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_consultations", x => x.consultation_id);
                    table.CheckConstraint("ck_duration_minutes_vc", "duration_minutes >= 0 OR duration_minutes IS NULL");
                    table.ForeignKey(
                        name: "f_k_video_consultations_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "appointment_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "health_prediction_records",
                columns: table => new
                {
                    prediction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_prediction_records", x => new { x.prediction_id, x.record_id });
                    table.ForeignKey(
                        name: "f_k_health_prediction_records_health_predictions_prediction_id",
                        column: x => x.prediction_id,
                        principalTable: "health_predictions",
                        principalColumn: "prediction_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_health_prediction_records_health_records_record_id",
                        column: x => x.record_id,
                        principalTable: "health_records",
                        principalColumn: "record_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "video_consultation_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consultation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    left_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_consultation_participants", x => x.id);
                    table.CheckConstraint("ck_left_after_joined", "left_at > joined_at OR left_at IS NULL");
                    table.CheckConstraint("ck_participant_identity", "patient_id IS NOT NULL OR doctor_id IS NOT NULL");
                    table.ForeignKey(
                        name: "f_k_video_consultation_participants_video_consultations_consult~",
                        column: x => x.consultation_id,
                        principalTable: "video_consultations",
                        principalColumn: "consultation_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_appointments_approved_by_admin_id",
                table: "appointments",
                column: "approved_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "i_x_appointments_center_id_appointment_date",
                table: "appointments",
                columns: new[] { "center_id", "appointment_date" });

            migrationBuilder.CreateIndex(
                name: "i_x_appointments_doctor_id_appointment_date",
                table: "appointments",
                columns: new[] { "doctor_id", "appointment_date" });

            migrationBuilder.CreateIndex(
                name: "i_x_appointments_patient_id_appointment_date",
                table: "appointments",
                columns: new[] { "patient_id", "appointment_date" });

            migrationBuilder.CreateIndex(
                name: "i_x_appointments_status",
                table: "appointments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_appointments_no_double_booking",
                table: "appointments",
                columns: new[] { "doctor_id", "center_id", "appointment_date", "appointment_time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_doctor_healthcare_centers_center_id",
                table: "doctor_healthcare_centers",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "i_x_doctor_healthcare_centers_doctor_id_center_id",
                table: "doctor_healthcare_centers",
                columns: new[] { "doctor_id", "center_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_doctor_schedules_center_id",
                table: "doctor_schedules",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "i_x_doctor_schedules_doctor_id_center_id",
                table: "doctor_schedules",
                columns: new[] { "doctor_id", "center_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_doctors_badge_number",
                table: "doctors",
                column: "badge_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_doctors_license_number",
                table: "doctors",
                column: "license_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_doctors_specialization",
                table: "doctors",
                column: "specialization");

            migrationBuilder.CreateIndex(
                name: "i_x_health_prediction_records_prediction_id_record_id",
                table: "health_prediction_records",
                columns: new[] { "prediction_id", "record_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_health_prediction_records_record_id",
                table: "health_prediction_records",
                column: "record_id");

            migrationBuilder.CreateIndex(
                name: "i_x_health_predictions_patient_id_prediction_date",
                table: "health_predictions",
                columns: new[] { "patient_id", "prediction_date" });

            migrationBuilder.CreateIndex(
                name: "i_x_health_records_patient_id_record_date",
                table: "health_records",
                columns: new[] { "patient_id", "record_date" });

            migrationBuilder.CreateIndex(
                name: "i_x_healthcare_center_admins_center_id",
                table: "healthcare_center_admins",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "i_x_healthcare_centers_city_region",
                table: "healthcare_centers",
                columns: new[] { "city", "region" });

            migrationBuilder.CreateIndex(
                name: "i_x_healthcare_centers_license_number",
                table: "healthcare_centers",
                column: "license_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_healthcare_centers_subscription_status",
                table: "healthcare_centers",
                column: "subscription_status");

            migrationBuilder.CreateIndex(
                name: "i_x_otp_verifications_phone_number_purpose_is_used",
                table: "otp_verifications",
                columns: new[] { "phone_number", "purpose", "is_used" });

            migrationBuilder.CreateIndex(
                name: "i_x_payments_appointment_id",
                table: "payments",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "i_x_payments_patient_id",
                table: "payments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "i_x_payments_payment_ref",
                table: "payments",
                column: "payment_ref",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_payments_status",
                table: "payments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "i_x_prescriptions_appointment_id",
                table: "prescriptions",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "i_x_prescriptions_center_id",
                table: "prescriptions",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "i_x_prescriptions_doctor_id",
                table: "prescriptions",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "i_x_prescriptions_patient_id",
                table: "prescriptions",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "i_x_queue_appointment_id",
                table: "queue",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_queue_center_id_position",
                table: "queue",
                columns: new[] { "center_id", "position" });

            migrationBuilder.CreateIndex(
                name: "i_x_queue_center_id_queue_date_queue_number",
                table: "queue",
                columns: new[] { "center_id", "queue_date", "queue_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_queue_center_id_queue_date_status",
                table: "queue",
                columns: new[] { "center_id", "queue_date", "status" });

            migrationBuilder.CreateIndex(
                name: "i_x_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_users_phone_number",
                table: "users",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_users_user_type_status",
                table: "users",
                columns: new[] { "user_type", "status" });

            migrationBuilder.CreateIndex(
                name: "i_x_video_consultation_participants_consultation_id",
                table: "video_consultation_participants",
                column: "consultation_id");

            migrationBuilder.CreateIndex(
                name: "i_x_video_consultations_appointment_id",
                table: "video_consultations",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_video_consultations_room_id",
                table: "video_consultations",
                column: "room_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doctor_healthcare_centers");

            migrationBuilder.DropTable(
                name: "doctor_schedules");

            migrationBuilder.DropTable(
                name: "health_prediction_records");

            migrationBuilder.DropTable(
                name: "otp_verifications");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "prescriptions");

            migrationBuilder.DropTable(
                name: "queue");

            migrationBuilder.DropTable(
                name: "video_consultation_participants");

            migrationBuilder.DropTable(
                name: "health_predictions");

            migrationBuilder.DropTable(
                name: "health_records");

            migrationBuilder.DropTable(
                name: "video_consultations");

            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "healthcare_center_admins");

            migrationBuilder.DropTable(
                name: "doctors");

            migrationBuilder.DropTable(
                name: "patients");

            migrationBuilder.DropTable(
                name: "healthcare_centers");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
