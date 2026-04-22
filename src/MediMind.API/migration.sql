CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE healthcare_centers (
    center_id uuid NOT NULL,
    center_name character varying(200) NOT NULL,
    center_type character varying(50) NOT NULL,
    license_number character varying(50) NOT NULL,
    address character varying(500) NOT NULL,
    city character varying(100) NOT NULL,
    region character varying(100) NOT NULL,
    phone_number character varying(20) NOT NULL,
    email character varying(255) NOT NULL,
    working_hours jsonb NOT NULL,
    services_offered text NOT NULL,
    specializations text NOT NULL,
    subscription_status character varying(20) NOT NULL,
    subscription_start_date date,
    subscription_end_date date,
    slot_duration_minutes integer NOT NULL,
    advance_booking_days integer NOT NULL,
    cancellation_hours integer NOT NULL,
    auto_approve_appointments boolean NOT NULL,
    latitude numeric(10,8),
    longitude numeric(11,8),
    profile_image_url character varying(1024),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_healthcare_centers" PRIMARY KEY (center_id),
    CONSTRAINT ck_advance_booking CHECK (advance_booking_days BETWEEN 1 AND 90),
    CONSTRAINT ck_slot_duration CHECK (slot_duration_minutes IN (15, 30, 45, 60))
);

CREATE TABLE otp_verifications (
    id uuid NOT NULL,
    phone_number character varying(20) NOT NULL,
    code character varying(6) NOT NULL,
    purpose character varying(50) NOT NULL,
    expiration_time timestamp with time zone NOT NULL,
    is_used boolean NOT NULL DEFAULT FALSE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_otp_verifications" PRIMARY KEY (id)
);

CREATE TABLE users (
    user_id uuid NOT NULL,
    email character varying(255) NOT NULL,
    phone_number character varying(20) NOT NULL,
    full_name character varying(100) NOT NULL,
    date_of_birth date NOT NULL,
    gender character varying(20) NOT NULL,
    user_type character varying(20) NOT NULL,
    status character varying(20) NOT NULL,
    profile_image_url character varying(1024),
    password_hash character varying(255) NOT NULL,
    last_login timestamp with time zone,
    is_verified boolean NOT NULL,
    otp_code text,
    otp_expires_at timestamp with time zone,
    otp_attempts integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_users" PRIMARY KEY (user_id)
);

CREATE TABLE doctors (
    user_id uuid NOT NULL,
    badge_number character varying(6) NOT NULL,
    specialization character varying(100) NOT NULL,
    license_number character varying(50) NOT NULL,
    years_of_experience integer NOT NULL,
    qualifications text,
    languages_spoken text NOT NULL,
    CONSTRAINT "PK_doctors" PRIMARY KEY (user_id),
    CONSTRAINT "FK_doctors_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
);

CREATE TABLE healthcare_center_admins (
    user_id uuid NOT NULL,
    center_id uuid NOT NULL,
    role text NOT NULL,
    department text,
    is_active boolean NOT NULL,
    CONSTRAINT "PK_healthcare_center_admins" PRIMARY KEY (user_id),
    CONSTRAINT "FK_healthcare_center_admins_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE,
    CONSTRAINT f_k_healthcare_center_admins_healthcare_centers_center_id FOREIGN KEY (center_id) REFERENCES healthcare_centers (center_id) ON DELETE RESTRICT
);

CREATE TABLE patients (
    user_id uuid NOT NULL,
    address character varying(300),
    blood_type character varying(5),
    allergies text,
    emergency_contact_name text,
    emergency_contact_phone text,
    medical_history text,
    chronic_conditions text NOT NULL,
    current_medications text NOT NULL,
    CONSTRAINT "PK_patients" PRIMARY KEY (user_id),
    CONSTRAINT "FK_patients_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
);

CREATE TABLE doctor_healthcare_centers (
    id uuid NOT NULL,
    doctor_id uuid NOT NULL,
    center_id uuid NOT NULL,
    consultation_fee numeric(10,2) NOT NULL,
    joined_date date NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_doctor_healthcare_centers" PRIMARY KEY (id),
    CONSTRAINT ck_consultation_fee CHECK (consultation_fee > 0),
    CONSTRAINT f_k_doctor_healthcare_centers_doctors_doctor_id FOREIGN KEY (doctor_id) REFERENCES doctors (user_id) ON DELETE RESTRICT,
    CONSTRAINT f_k_doctor_healthcare_centers_healthcare_centers_center_id FOREIGN KEY (center_id) REFERENCES healthcare_centers (center_id) ON DELETE RESTRICT
);

CREATE TABLE doctor_schedules (
    id uuid NOT NULL,
    doctor_id uuid NOT NULL,
    center_id uuid NOT NULL,
    working_days text NOT NULL,
    start_time time without time zone NOT NULL,
    end_time time without time zone NOT NULL,
    slot_duration integer NOT NULL,
    break_start time without time zone,
    break_end time without time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_doctor_schedules" PRIMARY KEY (id),
    CONSTRAINT ck_break_times CHECK (break_end > break_start OR break_start IS NULL),
    CONSTRAINT ck_schedule_times CHECK (end_time > start_time),
    CONSTRAINT f_k_doctor_schedules_doctors_doctor_id FOREIGN KEY (doctor_id) REFERENCES doctors (user_id) ON DELETE RESTRICT,
    CONSTRAINT f_k_doctor_schedules_healthcare_centers_center_id FOREIGN KEY (center_id) REFERENCES healthcare_centers (center_id) ON DELETE CASCADE
);

CREATE TABLE appointments (
    appointment_id uuid NOT NULL,
    patient_id uuid NOT NULL,
    doctor_id uuid NOT NULL,
    center_id uuid NOT NULL,
    appointment_date date NOT NULL,
    appointment_time time without time zone NOT NULL,
    duration_minutes integer NOT NULL DEFAULT 30,
    status character varying(20) NOT NULL,
    reason_for_visit text NOT NULL,
    symptoms text,
    booking_date timestamp with time zone NOT NULL,
    approved_by uuid,
    approved_at timestamp with time zone,
    cancellation_reason text,
    cancelled_by uuid,
    cancelled_at timestamp with time zone,
    check_in_time timestamp with time zone,
    check_out_time timestamp with time zone,
    notes text,
    approved_by_admin_id uuid,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_appointments" PRIMARY KEY (appointment_id),
    CONSTRAINT ck_duration_minutes CHECK (duration_minutes IN (15, 30, 45, 60)),
    CONSTRAINT f_k_appointments__healthcare_center_admins_approved_by_admin_id FOREIGN KEY (approved_by_admin_id) REFERENCES healthcare_center_admins (user_id),
    CONSTRAINT f_k_appointments_doctors_doctor_id FOREIGN KEY (doctor_id) REFERENCES doctors (user_id) ON DELETE RESTRICT,
    CONSTRAINT f_k_appointments_healthcare_centers_center_id FOREIGN KEY (center_id) REFERENCES healthcare_centers (center_id) ON DELETE RESTRICT,
    CONSTRAINT f_k_appointments_patients_patient_id FOREIGN KEY (patient_id) REFERENCES patients (user_id) ON DELETE RESTRICT
);

CREATE TABLE health_predictions (
    prediction_id uuid NOT NULL,
    patient_id uuid NOT NULL,
    prediction_date date NOT NULL,
    prediction_time time without time zone NOT NULL,
    diabetes_risk numeric(5,2) NOT NULL,
    diabetes_category character varying(20) NOT NULL,
    hypertension_risk numeric(5,2) NOT NULL,
    hypertension_category character varying(20) NOT NULL,
    cvd_risk numeric(5,2) NOT NULL,
    cvd_category character varying(20) NOT NULL,
    model_version character varying(20) NOT NULL,
    confidence numeric(5,2) NOT NULL,
    contributing_factors jsonb NOT NULL,
    recommendations text NOT NULL,
    data_points_used integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_health_predictions" PRIMARY KEY (prediction_id),
    CONSTRAINT ck_data_points CHECK (data_points_used > 0),
    CONSTRAINT f_k_health_predictions_patients_patient_id FOREIGN KEY (patient_id) REFERENCES patients (user_id) ON DELETE CASCADE
);

CREATE TABLE health_records (
    record_id uuid NOT NULL,
    patient_id uuid NOT NULL,
    record_date date NOT NULL,
    record_time time without time zone NOT NULL,
    systolic_bp integer,
    diastolic_bp integer,
    glucose_level numeric(5,2),
    weight numeric(5,2),
    height numeric(5,2),
    temperature numeric(4,2),
    heart_rate integer,
    oxygen_saturation integer,
    respiratory_rate integer,
    notes text,
    recorded_by character varying(50) NOT NULL DEFAULT 'patient',
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_health_records" PRIMARY KEY (record_id),
    CONSTRAINT ck_bp_relation CHECK (systolic_bp > diastolic_bp OR systolic_bp IS NULL OR diastolic_bp IS NULL),
    CONSTRAINT ck_diastolic_bp CHECK (diastolic_bp BETWEEN 40 AND 150 OR diastolic_bp IS NULL),
    CONSTRAINT ck_glucose CHECK (glucose_level BETWEEN 30 AND 600 OR glucose_level IS NULL),
    CONSTRAINT ck_heart_rate CHECK (heart_rate BETWEEN 30 AND 250 OR heart_rate IS NULL),
    CONSTRAINT ck_systolic_bp CHECK (systolic_bp BETWEEN 70 AND 250 OR systolic_bp IS NULL),
    CONSTRAINT ck_temperature CHECK (temperature BETWEEN 35 AND 43 OR temperature IS NULL),
    CONSTRAINT f_k_health_records_patients_patient_id FOREIGN KEY (patient_id) REFERENCES patients (user_id) ON DELETE CASCADE
);

CREATE TABLE payments (
    payment_id uuid NOT NULL,
    appointment_id uuid NOT NULL,
    patient_id uuid NOT NULL,
    payment_ref character varying(100) NOT NULL,
    amount numeric(10,2) NOT NULL,
    payment_date timestamp with time zone NOT NULL,
    payment_method character varying(50) NOT NULL,
    status character varying(20) NOT NULL,
    chapa_transaction_id character varying(100),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_payments" PRIMARY KEY (payment_id),
    CONSTRAINT ck_payment_amount CHECK (amount > 0),
    CONSTRAINT f_k_payments_appointments_appointment_id FOREIGN KEY (appointment_id) REFERENCES appointments (appointment_id) ON DELETE RESTRICT,
    CONSTRAINT f_k_payments_patients_patient_id FOREIGN KEY (patient_id) REFERENCES patients (user_id) ON DELETE RESTRICT
);

CREATE TABLE prescriptions (
    prescription_id uuid NOT NULL,
    appointment_id uuid NOT NULL,
    patient_id uuid NOT NULL,
    doctor_id uuid NOT NULL,
    center_id uuid NOT NULL,
    issue_date date NOT NULL,
    expiry_date date,
    diagnosis text NOT NULL,
    medications jsonb NOT NULL,
    lab_tests text NOT NULL,
    follow_up_instructions text,
    special_instructions text,
    prescription_url character varying(1024),
    qr_code text,
    status character varying(20) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_prescriptions" PRIMARY KEY (prescription_id),
    CONSTRAINT ck_expiry_date CHECK (expiry_date > issue_date OR expiry_date IS NULL),
    CONSTRAINT f_k_prescriptions_appointments_appointment_id FOREIGN KEY (appointment_id) REFERENCES appointments (appointment_id) ON DELETE RESTRICT,
    CONSTRAINT f_k_prescriptions_doctors_doctor_id FOREIGN KEY (doctor_id) REFERENCES doctors (user_id) ON DELETE RESTRICT,
    CONSTRAINT f_k_prescriptions_healthcare_centers_center_id FOREIGN KEY (center_id) REFERENCES healthcare_centers (center_id) ON DELETE CASCADE,
    CONSTRAINT f_k_prescriptions_patients_patient_id FOREIGN KEY (patient_id) REFERENCES patients (user_id) ON DELETE CASCADE
);

CREATE TABLE queue (
    queue_id uuid NOT NULL,
    appointment_id uuid NOT NULL,
    center_id uuid NOT NULL,
    queue_date date NOT NULL,
    queue_number character varying(10) NOT NULL,
    position integer NOT NULL,
    status character varying(20) NOT NULL,
    estimated_wait_time_minutes integer NOT NULL,
    called_time timestamp with time zone,
    consultation_start_time timestamp with time zone,
    consultation_end_time timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_queue" PRIMARY KEY (queue_id),
    CONSTRAINT ck_estimated_wait CHECK (estimated_wait_time_minutes >= 0),
    CONSTRAINT ck_queue_position CHECK (position > 0),
    CONSTRAINT f_k_queue_appointments_appointment_id FOREIGN KEY (appointment_id) REFERENCES appointments (appointment_id) ON DELETE CASCADE,
    CONSTRAINT f_k_queue_healthcare_centers_center_id FOREIGN KEY (center_id) REFERENCES healthcare_centers (center_id) ON DELETE RESTRICT
);

CREATE TABLE video_consultations (
    consultation_id uuid NOT NULL,
    appointment_id uuid NOT NULL,
    room_id character varying(100) NOT NULL,
    status character varying(20) NOT NULL,
    start_time timestamp with time zone,
    end_time timestamp with time zone,
    duration_minutes integer,
    video_quality character varying(20),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_video_consultations" PRIMARY KEY (consultation_id),
    CONSTRAINT ck_duration_minutes_vc CHECK (duration_minutes >= 0 OR duration_minutes IS NULL),
    CONSTRAINT f_k_video_consultations_appointments_appointment_id FOREIGN KEY (appointment_id) REFERENCES appointments (appointment_id) ON DELETE RESTRICT
);

CREATE TABLE health_prediction_records (
    prediction_id uuid NOT NULL,
    record_id uuid NOT NULL,
    CONSTRAINT "PK_health_prediction_records" PRIMARY KEY (prediction_id, record_id),
    CONSTRAINT f_k_health_prediction_records_health_predictions_prediction_id FOREIGN KEY (prediction_id) REFERENCES health_predictions (prediction_id) ON DELETE CASCADE,
    CONSTRAINT f_k_health_prediction_records_health_records_record_id FOREIGN KEY (record_id) REFERENCES health_records (record_id) ON DELETE CASCADE
);

CREATE TABLE video_consultation_participants (
    id uuid NOT NULL,
    consultation_id uuid NOT NULL,
    patient_id uuid,
    doctor_id uuid,
    joined_at timestamp with time zone,
    left_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_video_consultation_participants" PRIMARY KEY (id),
    CONSTRAINT ck_left_after_joined CHECK (left_at > joined_at OR left_at IS NULL),
    CONSTRAINT ck_participant_identity CHECK (patient_id IS NOT NULL OR doctor_id IS NOT NULL),
    CONSTRAINT "f_k_video_consultation_participants_video_consultations_consult~" FOREIGN KEY (consultation_id) REFERENCES video_consultations (consultation_id) ON DELETE CASCADE
);

CREATE INDEX i_x_appointments_approved_by_admin_id ON appointments (approved_by_admin_id);

CREATE INDEX i_x_appointments_center_id_appointment_date ON appointments (center_id, appointment_date);

CREATE INDEX i_x_appointments_doctor_id_appointment_date ON appointments (doctor_id, appointment_date);

CREATE INDEX i_x_appointments_patient_id_appointment_date ON appointments (patient_id, appointment_date);

CREATE INDEX i_x_appointments_status ON appointments (status);

CREATE UNIQUE INDEX idx_appointments_no_double_booking ON appointments (doctor_id, center_id, appointment_date, appointment_time);

CREATE INDEX i_x_doctor_healthcare_centers_center_id ON doctor_healthcare_centers (center_id);

CREATE UNIQUE INDEX i_x_doctor_healthcare_centers_doctor_id_center_id ON doctor_healthcare_centers (doctor_id, center_id);

CREATE INDEX i_x_doctor_schedules_center_id ON doctor_schedules (center_id);

CREATE UNIQUE INDEX i_x_doctor_schedules_doctor_id_center_id ON doctor_schedules (doctor_id, center_id);

CREATE UNIQUE INDEX i_x_doctors_badge_number ON doctors (badge_number);

CREATE UNIQUE INDEX i_x_doctors_license_number ON doctors (license_number);

CREATE INDEX i_x_doctors_specialization ON doctors (specialization);

CREATE UNIQUE INDEX i_x_health_prediction_records_prediction_id_record_id ON health_prediction_records (prediction_id, record_id);

CREATE INDEX i_x_health_prediction_records_record_id ON health_prediction_records (record_id);

CREATE INDEX i_x_health_predictions_patient_id_prediction_date ON health_predictions (patient_id, prediction_date);

CREATE INDEX i_x_health_records_patient_id_record_date ON health_records (patient_id, record_date);

CREATE INDEX i_x_healthcare_center_admins_center_id ON healthcare_center_admins (center_id);

CREATE INDEX i_x_healthcare_centers_city_region ON healthcare_centers (city, region);

CREATE UNIQUE INDEX i_x_healthcare_centers_license_number ON healthcare_centers (license_number);

CREATE INDEX i_x_healthcare_centers_subscription_status ON healthcare_centers (subscription_status);

CREATE INDEX i_x_otp_verifications_phone_number_purpose_is_used ON otp_verifications (phone_number, purpose, is_used);

CREATE INDEX i_x_payments_appointment_id ON payments (appointment_id);

CREATE INDEX i_x_payments_patient_id ON payments (patient_id);

CREATE UNIQUE INDEX i_x_payments_payment_ref ON payments (payment_ref);

CREATE INDEX i_x_payments_status ON payments (status);

CREATE INDEX i_x_prescriptions_appointment_id ON prescriptions (appointment_id);

CREATE INDEX i_x_prescriptions_center_id ON prescriptions (center_id);

CREATE INDEX i_x_prescriptions_doctor_id ON prescriptions (doctor_id);

CREATE INDEX i_x_prescriptions_patient_id ON prescriptions (patient_id);

CREATE UNIQUE INDEX i_x_queue_appointment_id ON queue (appointment_id);

CREATE INDEX i_x_queue_center_id_position ON queue (center_id, position);

CREATE UNIQUE INDEX i_x_queue_center_id_queue_date_queue_number ON queue (center_id, queue_date, queue_number);

CREATE INDEX i_x_queue_center_id_queue_date_status ON queue (center_id, queue_date, status);

CREATE UNIQUE INDEX i_x_users_email ON users (email);

CREATE UNIQUE INDEX i_x_users_phone_number ON users (phone_number);

CREATE INDEX i_x_users_user_type_status ON users (user_type, status);

CREATE INDEX i_x_video_consultation_participants_consultation_id ON video_consultation_participants (consultation_id);

CREATE UNIQUE INDEX i_x_video_consultations_appointment_id ON video_consultations (appointment_id);

CREATE UNIQUE INDEX i_x_video_consultations_room_id ON video_consultations (room_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413121715_InitialCreate', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE healthcare_center_admins ALTER COLUMN center_id DROP NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413122436_MakeAdminCenterOptional', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE healthcare_center_admins ALTER COLUMN center_id DROP NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413122457_SyncModelAfterAdminCenterOptional', '10.0.4');

COMMIT;

START TRANSACTION;
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'users'
          AND column_name = 'is_verified'
    ) THEN
        ALTER TABLE users ADD is_verified boolean NOT NULL DEFAULT FALSE;
    END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413122645_AddMissingUserColumns', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE patients ALTER COLUMN blood_type TYPE character varying(20);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413132132_WidenPatientBloodType', '10.0.4');

COMMIT;

START TRANSACTION;
DROP INDEX i_x_health_records_patient_id_record_date;

ALTER TABLE health_records ALTER COLUMN record_time SET DEFAULT (CURRENT_TIME);

ALTER TABLE health_records ALTER COLUMN created_at SET DEFAULT (TIMEZONE('utc', NOW()));

CREATE INDEX i_x_health_records_patient_id_record_date ON health_records (patient_id, record_date DESC);

ALTER TABLE health_records ADD CONSTRAINT ck_height CHECK (height BETWEEN 50 AND 250 OR height IS NULL);

ALTER TABLE health_records ADD CONSTRAINT ck_oxygen_saturation CHECK (oxygen_saturation BETWEEN 70 AND 100 OR oxygen_saturation IS NULL);

ALTER TABLE health_records ADD CONSTRAINT ck_recorded_by CHECK (recorded_by IN ('patient', 'doctor'));

ALTER TABLE health_records ADD CONSTRAINT ck_respiratory_rate CHECK (respiratory_rate BETWEEN 8 AND 60 OR respiratory_rate IS NULL);

ALTER TABLE health_records ADD CONSTRAINT ck_weight CHECK (weight BETWEEN 20 AND 300 OR weight IS NULL);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260418081844_UpdateModel', '10.0.4');

COMMIT;

START TRANSACTION;
DROP INDEX i_x_health_predictions_patient_id_prediction_date;

ALTER TABLE health_prediction_records DROP CONSTRAINT "PK_health_prediction_records";

ALTER TABLE payments ALTER COLUMN payment_date DROP NOT NULL;

ALTER TABLE payments ALTER COLUMN created_at SET DEFAULT (TIMEZONE('utc', NOW()));

ALTER TABLE payments ADD chapa_checkout_url character varying(2000);

ALTER TABLE payments ADD receipt_url character varying(2000);

ALTER TABLE payments ADD webhook_received_at timestamp with time zone;

ALTER TABLE healthcare_centers ADD patient_id uuid;

ALTER TABLE health_predictions ALTER COLUMN created_at SET DEFAULT (TIMEZONE('utc', NOW()));

ALTER TABLE health_prediction_records ADD id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE appointments ALTER COLUMN booking_date SET DEFAULT (TIMEZONE('utc', NOW()));

ALTER TABLE appointments ADD original_appointment_id uuid;

ALTER TABLE appointments ADD reminder24h_sent_at timestamp with time zone;

ALTER TABLE appointments ADD reminder2h_sent_at timestamp with time zone;

ALTER TABLE appointments ADD reschedule_count integer NOT NULL DEFAULT 0;

ALTER TABLE health_prediction_records ADD CONSTRAINT "PK_health_prediction_records" PRIMARY KEY (id);

CREATE UNIQUE INDEX i_x_payments_chapa_transaction_id ON payments (chapa_transaction_id) WHERE chapa_transaction_id IS NOT NULL;

CREATE INDEX i_x_healthcare_centers_patient_id ON healthcare_centers (patient_id);

CREATE INDEX i_x_health_predictions_patient_id_prediction_date ON health_predictions (patient_id, prediction_date DESC);

ALTER TABLE appointments ADD CONSTRAINT ck_appointment_date_not_past CHECK (appointment_date >= CURRENT_DATE);

ALTER TABLE healthcare_centers ADD CONSTRAINT f_k_healthcare_centers_patients_patient_id FOREIGN KEY (patient_id) REFERENCES patients (user_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260418140533_PaymentBillingChapaIntegration', '10.0.4');

COMMIT;

START TRANSACTION;
CREATE TABLE chat_messages (
    message_id uuid NOT NULL,
    consultation_id uuid NOT NULL,
    sender_id uuid NOT NULL,
    sender_type character varying(20) NOT NULL,
    content character varying(2000) NOT NULL,
    sent_at timestamp with time zone NOT NULL DEFAULT (TIMEZONE('utc', NOW())),
    is_read boolean NOT NULL DEFAULT FALSE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_chat_messages" PRIMARY KEY (message_id),
    CONSTRAINT f_k_chat_messages_video_consultations_consultation_id FOREIGN KEY (consultation_id) REFERENCES video_consultations (consultation_id) ON DELETE CASCADE
);

CREATE TABLE video_quality_metrics (
    id uuid NOT NULL,
    consultation_id uuid NOT NULL,
    user_id uuid NOT NULL,
    bandwidth_kbps integer NOT NULL,
    packets_lost integer NOT NULL,
    frame_rate integer NOT NULL,
    reported_at timestamp with time zone NOT NULL DEFAULT (TIMEZONE('utc', NOW())),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_video_quality_metrics" PRIMARY KEY (id),
    CONSTRAINT f_k_video_quality_metrics_video_consultations_consultation_id FOREIGN KEY (consultation_id) REFERENCES video_consultations (consultation_id) ON DELETE CASCADE
);

CREATE INDEX i_x_chat_messages_consultation_id_sent_at ON chat_messages (consultation_id, sent_at DESC);

CREATE INDEX i_x_video_quality_metrics_consultation_id_reported_at ON video_quality_metrics (consultation_id, reported_at);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260418141533_TelemedicineVideoSignalingChat', '10.0.4');

COMMIT;

START TRANSACTION;
CREATE TABLE medication_reminders (
    reminder_id uuid NOT NULL,
    patient_id uuid NOT NULL,
    medication_name character varying(200) NOT NULL,
    dosage character varying(200) NOT NULL,
    frequency character varying(20) NOT NULL,
    reminder_times text NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_medication_reminders" PRIMARY KEY (reminder_id),
    CONSTRAINT f_k_medication_reminders_patients_patient_id FOREIGN KEY (patient_id) REFERENCES patients (user_id) ON DELETE CASCADE
);

CREATE TABLE notification_logs (
    log_id uuid NOT NULL,
    user_id uuid,
    phone_number character varying(20),
    notification_type character varying(80) NOT NULL,
    channel character varying(20) NOT NULL,
    title character varying(200),
    body character varying(4000) NOT NULL,
    status character varying(20) NOT NULL,
    external_reference character varying(512),
    error_message character varying(2000),
    sent_at timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_notification_logs" PRIMARY KEY (log_id)
);

CREATE TABLE user_device_tokens (
    token_id uuid NOT NULL,
    user_id uuid NOT NULL,
    fcm_token character varying(512) NOT NULL,
    device_platform character varying(20) NOT NULL,
    device_model character varying(200),
    registered_at timestamp with time zone NOT NULL,
    last_used_at timestamp with time zone NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_user_device_tokens" PRIMARY KEY (token_id),
    CONSTRAINT f_k_user_device_tokens_users_user_id FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
);

CREATE INDEX i_x_medication_reminders_patient_id_is_active ON medication_reminders (patient_id, is_active);

CREATE INDEX i_x_notification_logs_user_id_sent_at ON notification_logs (user_id, sent_at);

CREATE UNIQUE INDEX i_x_user_device_tokens_user_id_fcm_token ON user_device_tokens (user_id, fcm_token);

CREATE INDEX i_x_user_device_tokens_user_id_is_active ON user_device_tokens (user_id, is_active);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260419084838_AddNotificationSystem', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE prescriptions DROP CONSTRAINT f_k_prescriptions_healthcare_centers_center_id;

ALTER TABLE prescriptions DROP CONSTRAINT f_k_prescriptions_patients_patient_id;

DROP INDEX i_x_prescriptions_appointment_id;

CREATE UNIQUE INDEX i_x_prescriptions_appointment_id ON prescriptions (appointment_id);

ALTER TABLE prescriptions ADD CONSTRAINT f_k_prescriptions_healthcare_centers_center_id FOREIGN KEY (center_id) REFERENCES healthcare_centers (center_id) ON DELETE RESTRICT;

ALTER TABLE prescriptions ADD CONSTRAINT f_k_prescriptions_patients_patient_id FOREIGN KEY (patient_id) REFERENCES patients (user_id) ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260419092028_PrescriptionConstraintsAndPatientNav', '10.0.4');

COMMIT;

