using MediMind.Domain.Entities;

namespace MediMind.Domain.Common.Interfaces;

// ─── Current User ─────────────────────────────────────────────────────────────

/// <summary>
/// Provides the currently authenticated user's context from JWT claims.
/// Injected into command/query handlers for tenant isolation enforcement.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    string UserType { get; }  // Patient | Doctor | Admin | SuperAdmin
    Guid? TenantId { get; }   // center_id for Admins, null for Patients/Doctors
    bool IsAuthenticated { get; }
}

// ─── Authentication ───────────────────────────────────────────────────────────

public interface ITokenService
{
    /// <summary>Generates JWT access token (15 min) + refresh token (7 days).</summary>
    (string AccessToken, string RefreshToken) GenerateTokens(Guid userId, string userType, Guid? tenantId);
    bool ValidateRefreshToken(string refreshToken, out Guid userId);
    void RevokeRefreshToken(Guid userId);
}

public interface IPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

public interface IOtpService
{
    string GenerateOtp();  // 6-digit code
}

public interface IJwtService
{
    (string AccessToken, string RefreshToken) GenerateTokens(Guid userId, string role, Guid? tenantId);
}

public interface IAuthService
{
    Task<(string AccessToken, string RefreshToken)> GenerateAuthTokensAsync(User user, CancellationToken ct = default);
}

// ─── Notifications ────────────────────────────────────────────────────────────

public interface ISmsService
{
    /// <summary>Sends SMS via Geez SMS Gateway (ETB 0.65/SMS).</summary>
    Task SendAsync(string phoneNumber, string message, CancellationToken ct = default);
    Task SendOtpAsync(string phoneNumber, string otpCode, CancellationToken ct = default);
    Task SendAppointmentReminderAsync(string phoneNumber, string doctorName, DateTime appointmentTime, CancellationToken ct = default);
}

public interface IPushNotificationService
{
    /// <summary>Sends push notification via Firebase Cloud Messaging.</summary>
    Task SendAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null, CancellationToken ct = default);
    Task SendToUserAsync(Guid userId, string title, string body, Dictionary<string, string>? data = null, CancellationToken ct = default);
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    Task SendWelcomeAsync(string to, string fullName, CancellationToken ct = default);
    Task SendDoctorInvitationAsync(string to, string doctorName, string centerName, string invitationLink, CancellationToken ct = default);
}

public interface INotificationService
{
    Task SendPushAsync(Guid userId, string title, string body, Dictionary<string, string>? data = null, CancellationToken ct = default);
    Task SendSmsAsync(string phoneNumber, string message, CancellationToken ct = default);
    Task SendBothAsync(
        Guid userId,
        string phoneNumber,
        string title,
        string body,
        string smsMessage,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default);
    Task SendToMultipleUsersAsync(IEnumerable<Guid> userIds, string title, string body, CancellationToken ct = default);
    Task RegisterDeviceTokenAsync(Guid userId, string fcmToken, string platform, string? deviceModel, CancellationToken ct = default);
    Task DeregisterDeviceTokenAsync(Guid userId, string fcmToken, CancellationToken ct = default);
    Task SendQueueUpdateToPatientAsync(Guid patientId, object status);
    Task BroadcastQueueRefreshAsync(Guid centerId, object dashboard);
    Task BroadcastQueueEventAsync(Guid centerId, string eventName, object payload);
}

// ─── ML Service Client ────────────────────────────────────────────────────────

public interface IMlPredictionService
{
    /// <summary>
    /// Calls the Python Flask ML microservice at /ml/predict.
    /// Returns risk scores for Diabetes, Hypertension, and CVD.
    /// </summary>
    Task<MlPredictionResult> PredictAsync(MlPredictionRequest request, CancellationToken ct = default);
}

public record MlPredictionRequest(
    double AvgSystolicBp,
    double AvgDiastolicBp,
    double AvgGlucoseLevel,
    double CurrentBmi,
    double WeightChange,
    int Age,
    string Gender,          // Male | Female
    int DataPointsCount,
    string ModelVersion = "v1.0.0"
);

public record MlPredictionResult(
    double DiabetesRisk,
    double HypertensionRisk,
    double CvdRisk,
    double Confidence,
    Dictionary<string, List<string>> ContributingFactors,
    string Recommendations,
    string ModelVersion
);

// ─── Payment ──────────────────────────────────────────────────────────────────

public interface IPaymentService
{
    /// <summary>Initiates payment via Chapa gateway. Returns checkout URL.</summary>
    Task<ChapaPaymentInitResponse> InitiateAsync(ChapaPaymentRequest request, CancellationToken ct = default);
    /// <summary>Verifies payment webhook callback using HMAC-SHA256 signature.</summary>
    Task<bool> VerifyWebhookAsync(string payload, string signature, CancellationToken ct = default);
    Task<ChapaTransactionStatus> GetTransactionStatusAsync(string txRef, CancellationToken ct = default);
}

public record ChapaPaymentRequest(
    string TxRef,
    decimal Amount,
    string Currency,        // ETB
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string CallbackUrl,
    string ReturnUrl,
    string Description
);

public record ChapaPaymentInitResponse(bool Success, string? CheckoutUrl, string? Message);
public record ChapaTransactionStatus(string Status, string? TxRef, decimal? Amount);

// ─── Storage ──────────────────────────────────────────────────────────────────

public interface IStorageService
{
    /// <summary>Uploads a file and returns a public HTTPS URL (or app-relative URL when using local disk fallback).</summary>
    Task<string> UploadAsync(Stream fileStream, string fileName, string containerName, CancellationToken ct = default);
    Task DeleteAsync(string fileUrl, CancellationToken ct = default);
}

// ─── PDF Generation ───────────────────────────────────────────────────────────

public interface IPdfService
{
    /// <summary>Loads prescription and related entities from the database.</summary>
    Task<byte[]> GeneratePrescriptionPdfAsync(Guid prescriptionId, CancellationToken ct = default);

    /// <summary>Builds a prescription PDF from in-memory entities (used for consistent layout + QR).</summary>
    Task<byte[]> GeneratePrescriptionPdfAsync(
        Prescription prescription,
        Patient patient,
        Doctor doctor,
        HealthcareCenter center,
        string qrCodeDataUrl,
        CancellationToken ct = default);
}

// ─── QR (prescription verification) ────────────────────────────────────────────

public interface IQrCodeService
{
    string ComputeVerificationRef(Guid prescriptionId, DateOnly issueDate);
    /// <summary>URL-safe Base64 of the verification JSON payload.</summary>
    string BuildQrPayloadString(Guid prescriptionId, DateOnly issueDate);
    string GenerateQrCodeBase64(string qrTextContent);
    bool VerifyQrToken(Guid prescriptionId, DateOnly issueDate, string token);
}

// ─── Prescription PDF file storage (local disk / future blob) ─────────────────

public interface IPrescriptionFileStorage
{
    Task<string> SavePrescriptionPdfAsync(Guid prescriptionId, byte[] pdfBytes, CancellationToken ct = default);
    Task<byte[]?> GetPrescriptionPdfAsync(Guid prescriptionId, CancellationToken ct = default);
}

// ─── Real-Time (SignalR) ──────────────────────────────────────────────────────

public interface IQueueHubService
{
    /// <summary>Broadcasts queue update to all connected clients of a center.</summary>
    Task BroadcastQueueUpdateAsync(Guid centerId, object queueData, CancellationToken ct = default);
    /// <summary>Notifies a specific patient about their queue position.</summary>
    Task NotifyPatientAsync(Guid patientId, string eventName, object data, CancellationToken ct = default);
}

// ─── Background Jobs ──────────────────────────────────────────────────────────

public interface IQueueGenerationJob
{
    /// <summary>Called daily at 06:00 AM Ethiopian time (03:00 AM UTC).</summary>
    Task GenerateDailyQueuesAsync(DateOnly date, CancellationToken ct = default);
}

public interface IAppointmentReminderJob
{
    Task SendRemindersAsync(CancellationToken ct = default);
}
