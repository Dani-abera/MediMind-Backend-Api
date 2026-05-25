namespace MediMind.Application.Features.Payments;

// ─── Payment configuration interface (implemented in Infrastructure) ──────────

public interface IPaymentConfiguration
{
    decimal VatPercent { get; }
    decimal ServicePercent { get; }
}

// ─── Fee calculation helper ───────────────────────────────────────────────────

public record PaymentAmountBreakdown(
    decimal BaseAmount,
    decimal VatFee,
    decimal ServiceFee,
    decimal TotalAmount,
    decimal VatPercent,
    decimal ServicePercent);

public static class PaymentHelper
{
    public static PaymentAmountBreakdown CalculatePrice(
        decimal baseAmount,
        decimal vatPercent,
        decimal servicePercent)
    {
        var vatFee     = Math.Round(baseAmount * (vatPercent / 100m), 2);
        var serviceFee = Math.Round(baseAmount * (servicePercent / 100m), 2);
        var total      = baseAmount + vatFee + serviceFee;
        return new(baseAmount, vatFee, serviceFee, total, vatPercent, servicePercent);
    }
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record AppointmentDetailsDto(
    string DoctorName,
    string CenterName,
    DateOnly AppointmentDate,
    TimeOnly AppointmentTime);

public sealed record PaymentInitiationDto(
    Guid PaymentId,
    string PaymentRef,
    decimal BaseAmount,
    decimal VatFee,
    decimal ServiceFee,
    decimal TotalAmount,
    string Currency,
    DateTime ExpiresAt,
    AppointmentDetailsDto AppointmentDetails,
    string PatientEmail,
    string PatientPhone,
    string PatientFirstName,
    string PatientLastName);

public sealed record PaymentStatusDto(
    Guid PaymentId,
    string PaymentRef,
    string Status,
    decimal Amount,
    DateTime? PaymentDate,
    string? PaymentMethod,
    string? ChapaTransactionId,
    string? ReceiptUrl,
    Guid? AppointmentId);

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
    Guid? AppointmentId,
    decimal Amount,
    string Status,
    DateTime CreatedAt,
    DateTime? PaymentDate,
    string? ReceiptUrl,
    string PatientName,
    string DoctorName,
    string? PaymentMethod);
