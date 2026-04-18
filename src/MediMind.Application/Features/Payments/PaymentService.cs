using System.Text.Json;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MediMind.Application.Features.Payments;

public interface IChapaClient
{
    Task<ChapaInitializeResponse?> InitializePaymentAsync(ChapaInitializeRequest req, CancellationToken ct = default);
    Task<ChapaVerifyResponse?> VerifyPaymentAsync(string txRef, CancellationToken ct = default);
}

public interface IChapaWebhookValidator
{
    bool ValidateSignature(string payload, string signature, string secret);
}

public interface IChapaConfiguration
{
    string WebhookSecret { get; }
    string CallbackUrl { get; }
    string ReturnUrl { get; }
}

public interface IPaymentService
{
    Task<PaymentInitiationDto> InitiatePaymentAsync(Guid appointmentId, Guid patientId, CancellationToken ct = default);
    Task ProcessWebhookAsync(string payload, string chapaSignature, CancellationToken ct = default);
    Task<PaymentStatusDto> GetPaymentStatusAsync(Guid paymentId, string userType, Guid userId, Guid? centerId, CancellationToken ct = default);
    Task<PaymentStatusDto> GetByAppointmentAsync(Guid appointmentId, string userType, Guid userId, Guid? centerId, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentHistoryItemDto>> GetHistoryAsync(string userType, Guid userId, Guid? centerId, int page, int pageSize, CancellationToken ct = default);
    Task<byte[]> GenerateReceiptAsync(Guid paymentId, CancellationToken ct = default);
}

public sealed class PaymentService(
    IAppointmentRepository appointmentRepository,
    IHealthcareCenterRepository healthcareCenterRepository,
    IPaymentRepository paymentRepository,
    IChapaClient chapaClient,
    IChapaWebhookValidator webhookValidator,
    IChapaConfiguration chapaConfiguration,
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<PaymentInitiationDto> InitiatePaymentAsync(Guid appointmentId, Guid patientId, CancellationToken ct = default)
    {
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);

        if (appointment.PatientId != patientId)
            throw new ForbiddenException("You can only initiate payments for your own appointments.");
        if (appointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            throw new DomainException("Payments are only allowed for pending or confirmed appointments.");

        var existingPayment = await paymentRepository.GetByAppointmentIdAsync(appointmentId);
        if (existingPayment is not null && existingPayment.Status == PaymentStatus.Completed)
            throw new DomainException("A completed payment already exists for this appointment.");

        var relations = await healthcareCenterRepository.GetDoctorsAsync(appointment.CenterId);
        var relation = relations.FirstOrDefault(x => x.DoctorId == appointment.DoctorId)
            ?? throw new DomainException("Doctor-center pricing configuration is missing.");

        var paymentRef = $"PAY{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        while (await paymentRepository.ExistsByRefAsync(paymentRef, ct))
            paymentRef = $"PAY{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        var payment = Payment.Initiate(
            appointment.Id,
            patientId,
            relation.ConsultationFee,
            "Mobile Money",
            paymentRef);

        await paymentRepository.CreateAsync(payment);

        var initialize = new ChapaInitializeRequest(
            Amount: payment.Amount,
            Currency: "ETB",
            Email: appointment.Patient.Email,
            FirstName: appointment.Patient.FullName.Split(' ').FirstOrDefault() ?? appointment.Patient.FullName,
            LastName: appointment.Patient.FullName.Split(' ').Skip(1).FirstOrDefault() ?? "Patient",
            TxRef: payment.PaymentRef,
            CallbackUrl: chapaConfiguration.CallbackUrl,
            ReturnUrl: chapaConfiguration.ReturnUrl,
            Customization: new ChapaCustomization("MediMind Appointment", $"Consultation with {appointment.Doctor.FullName}")
        );

        var initResponse = await chapaClient.InitializePaymentAsync(initialize, ct);
        if (initResponse is null || !string.Equals(initResponse.Status, "success", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(initResponse.Data?.CheckoutUrl))
        {
            payment.MarkFailed();
            await unitOfWork.SaveChangesAsync(ct);
            throw new PaymentException(initResponse?.Message ?? "Unable to initialize payment with Chapa.");
        }

        payment.SetCheckoutUrl(initResponse.Data.CheckoutUrl);
        await unitOfWork.SaveChangesAsync(ct);

        return new PaymentInitiationDto(
            payment.Id,
            payment.PaymentRef,
            payment.Amount,
            "ETB",
            payment.ChapaCheckoutUrl!,
            DateTime.UtcNow.AddMinutes(30),
            new AppointmentDetailsDto(
                appointment.Doctor.FullName,
                appointment.Center.CenterName,
                appointment.AppointmentDate,
                appointment.AppointmentTime));
    }

    public async Task ProcessWebhookAsync(string payload, string chapaSignature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chapaConfiguration.WebhookSecret))
            throw new UnauthorizedException("Webhook secret is not configured.");
        if (!webhookValidator.ValidateSignature(payload, chapaSignature, chapaConfiguration.WebhookSecret))
            throw new UnauthorizedException("Invalid webhook signature");

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var eventData = root.TryGetProperty("data", out var dataNode) ? dataNode : root;
        var txRef = eventData.TryGetProperty("tx_ref", out var txRefNode) ? txRefNode.GetString() : null;
        var status = eventData.TryGetProperty("status", out var statusNode) ? statusNode.GetString() : null;

        if (string.IsNullOrWhiteSpace(txRef))
        {
            logger.LogWarning("Chapa webhook ignored. Missing tx_ref. Payload: {Payload}", payload);
            return;
        }

        var payment = await paymentRepository.GetByRefAsync(txRef);
        if (payment is null)
        {
            logger.LogWarning("Chapa webhook payment not found for tx_ref={TxRef}", txRef);
            return;
        }

        if (payment.WebhookReceivedAt.HasValue)
        {
            logger.LogInformation("Duplicate webhook skipped for tx_ref={TxRef}", txRef);
            return;
        }

        if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
        {
            var verify = await chapaClient.VerifyPaymentAsync(txRef, ct);
            if (!string.Equals(verify?.Status, "success", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(verify?.Data?.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                payment.MarkFailed();
            }
            else
            {
                payment.Complete(verify?.Data?.FlwRef);
                if (payment.Appointment.Status == AppointmentStatus.Pending)
                    payment.Appointment.Approve(Guid.Empty);

                _ = Task.Run(async () =>
                {
                    try { await GenerateReceiptAsync(payment.Id, CancellationToken.None); }
                    catch (Exception ex) { logger.LogWarning(ex, "Receipt generation failed for payment {PaymentId}", payment.Id); }
                }, CancellationToken.None);
            }
        }
        else if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            payment.MarkFailed();
        }

        payment.MarkWebhookReceived();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<PaymentStatusDto> GetPaymentStatusAsync(Guid paymentId, string userType, Guid userId, Guid? centerId, CancellationToken ct = default)
    {
        var payment = await paymentRepository.GetByIdAsync(paymentId)
            ?? throw new NotFoundException(nameof(Payment), paymentId);
        EnsureReadAccess(payment, userType, userId, centerId);
        return MapStatus(payment);
    }

    public async Task<PaymentStatusDto> GetByAppointmentAsync(Guid appointmentId, string userType, Guid userId, Guid? centerId, CancellationToken ct = default)
    {
        var payment = await paymentRepository.GetByAppointmentIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Payment), appointmentId);
        EnsureReadAccess(payment, userType, userId, centerId);
        return MapStatus(payment);
    }

    public async Task<IReadOnlyList<PaymentHistoryItemDto>> GetHistoryAsync(string userType, Guid userId, Guid? centerId, int page, int pageSize, CancellationToken ct = default)
    {
        IEnumerable<Payment> payments = string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase)
            ? await paymentRepository.GetByPatientAsync(userId, page, pageSize)
            : await paymentRepository.GetByCenterAsync(centerId ?? Guid.Empty, page, pageSize);

        return payments.Select(p => new PaymentHistoryItemDto(
            p.Id,
            p.PaymentRef,
            p.AppointmentId,
            p.Amount,
            p.Status.ToString(),
            p.CreatedAt,
            p.PaymentDate,
            p.ReceiptUrl)).ToList();
    }

    public async Task<byte[]> GenerateReceiptAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await paymentRepository.GetByIdAsync(paymentId)
            ?? throw new NotFoundException(nameof(Payment), paymentId);
        if (payment.Status != PaymentStatus.Completed)
            throw new DomainException("Receipt can only be generated for completed payments.");

        QuestPDF.Settings.License = LicenseType.Community;
        var pdfBytes = BuildReceipt(payment).GeneratePdf();

        await using var stream = new MemoryStream(pdfBytes);
        var receiptPath = await storageService.UploadAsync(stream, $"receipt-{payment.PaymentRef}.pdf", "receipts", ct);
        payment.SetReceiptUrl(receiptPath);
        await paymentRepository.UpdateAsync(payment);
        await unitOfWork.SaveChangesAsync(ct);
        return pdfBytes;
    }

    private static IDocument BuildReceipt(Payment payment) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(32);
                page.Header().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Text("MediMind").Bold().FontSize(22);
                    col.Item().Text($"Center: {payment.Appointment.Center.CenterName}");
                });
                page.Content().PaddingVertical(16).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"Receipt #: {payment.PaymentRef}");
                    col.Item().Text($"Date: {payment.PaymentDate:yyyy-MM-dd HH:mm} UTC");
                    col.Item().Text($"Patient: {payment.Patient.FullName} ({payment.Patient.PhoneNumber})");
                    col.Item().Text($"Doctor: Dr. {payment.Appointment.Doctor.FullName} - {payment.Appointment.Doctor.Specialization}");
                    col.Item().Text($"Center: {payment.Appointment.Center.CenterName}, {payment.Appointment.Center.Address}");
                    col.Item().Text("Service: Appointment consultation");
                    col.Item().Text($"Amount: ETB {payment.Amount:F2}").Bold();
                    col.Item().Text($"Payment Method: {payment.PaymentMethod}");
                    col.Item().Text("Status: PAID ✓").FontColor(Colors.Green.Darken2);
                    col.Item().Text($"Transaction ID: {payment.ChapaTransactionId ?? "N/A"}");
                });
                page.Footer().AlignCenter().Text("Thank you for choosing MediMind");
            });
        });

    private static void EnsureReadAccess(Payment payment, string userType, Guid userId, Guid? centerId)
    {
        if (string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase) && payment.PatientId != userId)
            throw new ForbiddenException("Access denied.");
        if (string.Equals(userType, "Admin", StringComparison.OrdinalIgnoreCase) && payment.Appointment.CenterId != centerId)
            throw new ForbiddenException("Access denied.");
    }

    private static PaymentStatusDto MapStatus(Payment payment) =>
        new(
            payment.Id,
            payment.PaymentRef,
            payment.Status.ToString(),
            payment.Amount,
            payment.PaymentDate,
            payment.PaymentMethod,
            payment.ChapaTransactionId,
            payment.ReceiptUrl,
            payment.AppointmentId);
}

public sealed record ChapaInitializeRequest(
    decimal Amount,
    string Currency,
    string Email,
    string FirstName,
    string LastName,
    string TxRef,
    string CallbackUrl,
    string ReturnUrl,
    ChapaCustomization Customization);

public sealed record ChapaCustomization(string Title, string Description);
public sealed record ChapaInitializeResponse(string Message, string Status, ChapaInitializeData Data);
public sealed record ChapaInitializeData(string CheckoutUrl);

public sealed record ChapaVerifyResponse(string Message, string Status, ChapaVerifyData Data);
public sealed record ChapaVerifyData(
    string TxRef,
    string FlwRef,
    decimal Amount,
    string Currency,
    string Status,
    string PaymentType);
