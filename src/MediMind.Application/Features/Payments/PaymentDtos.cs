namespace MediMind.Application.Features.Payments;

public sealed record AppointmentDetailsDto(
    string DoctorName,
    string CenterName,
    DateOnly AppointmentDate,
    TimeOnly AppointmentTime);

public sealed record PaymentInitiationDto(
    Guid PaymentId,
    string PaymentRef,
    decimal Amount,
    string Currency,
    string CheckoutUrl,
    DateTime ExpiresAt,
    AppointmentDetailsDto AppointmentDetails);

public sealed record PaymentStatusDto(
    Guid PaymentId,
    string PaymentRef,
    string Status,
    decimal Amount,
    DateTime? PaymentDate,
    string? PaymentMethod,
    string? ChapaTransactionId,
    string? ReceiptUrl,
    Guid AppointmentId);

public sealed record PaymentReceiptDto(
    string PaymentRef,
    decimal Amount,
    string Status,
    string PatientName,
    string DoctorName,
    string CenterName,
    DateOnly AppointmentDate,
    TimeOnly AppointmentTime,
    DateTime PaymentDate,
    string? ChapaTransactionId,
    string? DownloadUrl);

public sealed record PaymentHistoryItemDto(
    Guid PaymentId,
    string PaymentRef,
    Guid AppointmentId,
    decimal Amount,
    string Status,
    DateTime CreatedAt,
    DateTime? PaymentDate,
    string? ReceiptUrl);
