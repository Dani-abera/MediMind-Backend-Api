using System.Text.Json;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using INotificationService = MediMind.Domain.Common.Interfaces.INotificationService;
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
    Task<PaymentStatusDto> SyncPaymentAsync(Guid paymentId, Guid patientId, CancellationToken ct = default);
    Task<byte[]> GenerateReceiptAsync(Guid paymentId, CancellationToken ct = default);
}

public sealed class PaymentService(
    IAppointmentRepository appointmentRepository,
    IHealthcareCenterRepository healthcareCenterRepository,
    IPaymentRepository paymentRepository,
    IChapaClient chapaClient,
    IChapaWebhookValidator webhookValidator,
    IChapaConfiguration chapaConfiguration,
    IPaymentConfiguration paymentConfiguration,
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    INotificationService notificationService,
    ILogger<PaymentService> logger,
    IServiceScopeFactory scopeFactory) : IPaymentService
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
            throw new DomainException("This appointment has already been paid.");

        if (existingPayment is not null && existingPayment.Status == PaymentStatus.Pending)
        {
            var existingNameParts = appointment.Patient.FullName
                .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new PaymentInitiationDto(
                existingPayment.Id,
                existingPayment.PaymentRef,
                existingPayment.BaseAmount,
                existingPayment.VatFee,
                existingPayment.ServiceFee,
                existingPayment.TotalAmount,
                "ETB",
                DateTime.UtcNow.AddMinutes(30),
                new AppointmentDetailsDto(
                    appointment.Doctor.FullName,
                    appointment.Center.CenterName,
                    appointment.AppointmentDate,
                    appointment.AppointmentTime),
                appointment.Patient.Email,
                appointment.Patient.PhoneNumber,
                existingNameParts.FirstOrDefault() ?? appointment.Patient.FullName,
                existingNameParts.ElementAtOrDefault(1) ?? "");
        }

        var relations = await healthcareCenterRepository.GetDoctorsAsync(appointment.CenterId);
        var relation = relations.FirstOrDefault(x => x.DoctorId == appointment.DoctorId && x.IsActive)
            ?? relations.FirstOrDefault(x => x.DoctorId == appointment.DoctorId)
            ?? throw new NotFoundException("DoctorHealthcareCenter",
                $"Doctor {appointment.DoctorId} at Center {appointment.CenterId}");

        if (relation.ConsultationFee <= 0)
            throw new DomainException("Consultation fee for this doctor is not configured. Please contact the healthcare center.");

        var paymentRef = $"APPT-{Guid.NewGuid():N}";
        while (await paymentRepository.ExistsByRefAsync(paymentRef, ct))
            paymentRef = $"APPT-{Guid.NewGuid():N}";

        var amounts = PaymentHelper.CalculatePrice(
            relation.ConsultationFee,
            paymentConfiguration.VatPercent,
            paymentConfiguration.ServicePercent);

        var payment = Payment.InitiateWithFees(
            appointment.Id,
            patientId,
            appointment.CenterId,
            amounts.BaseAmount,
            amounts.VatPercent,
            amounts.VatFee,
            amounts.ServicePercent,
            amounts.ServiceFee,
            amounts.TotalAmount,
            paymentRef);

        await paymentRepository.CreateAsync(payment);

        payment.Activities.Add(PaymentActivity.Create(
            payment.Id, PaymentAction.Charge, PaymentStatus.Pending,
            payment.PaymentRef, amounts.TotalAmount,
            gatewayMessage: "Native checkout initiated via Chapa SDK"));
        await unitOfWork.SaveChangesAsync(ct);

        var nameParts = appointment.Patient.FullName
            .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new PaymentInitiationDto(
            payment.Id,
            payment.PaymentRef,
            amounts.BaseAmount,
            amounts.VatFee,
            amounts.ServiceFee,
            amounts.TotalAmount,
            "ETB",
            DateTime.UtcNow.AddMinutes(30),
            new AppointmentDetailsDto(
                appointment.Doctor.FullName,
                appointment.Center.CenterName,
                appointment.AppointmentDate,
                appointment.AppointmentTime),
            appointment.Patient.Email,
            appointment.Patient.PhoneNumber,
            nameParts.FirstOrDefault() ?? appointment.Patient.FullName,
            nameParts.ElementAtOrDefault(1) ?? "");
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
            logger.LogError(
                "Chapa webhook received for unknown tx_ref={TxRef}. Possible orphaned transaction or replay attack. Manual review required.",
                txRef);
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
                payment.Activities.Add(PaymentActivity.Create(
                    payment.Id, PaymentAction.Charge, PaymentStatus.Failed,
                    payment.PaymentRef, payment.TotalAmount,
                    gatewayMessage: "Chapa verify returned non-success"));
            }
            else
            {
                payment.Complete(verify?.Data?.FlwRef);
                payment.Activities.Add(PaymentActivity.Create(
                    payment.Id, PaymentAction.Charge, PaymentStatus.Completed,
                    payment.PaymentRef, payment.TotalAmount,
                    chapaTransactionId: verify?.Data?.FlwRef,
                    paymentMethodRaw: verify?.Data?.PaymentType,
                    confirmedAt: DateTime.UtcNow));

                if (payment.ReasonType == PaymentReasonType.Appointment
                    && payment.Appointment != null
                    && payment.Appointment.Status == AppointmentStatus.Pending)
                {
                    payment.Appointment.Approve(Guid.Empty);
                }

                if (payment.ReasonType == PaymentReasonType.Appointment)
                {
                    var capturedPaymentId = payment.Id;
                    _ = Task.Run(async () =>
                    {
                        using var scope = scopeFactory.CreateScope();
                        var svc = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                        try
                        {
                            await svc.GenerateReceiptAsync(capturedPaymentId, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "PDF receipt generation failed for payment {PaymentId}. Sending SMS fallback.", capturedPaymentId);
                            try
                            {
                                var paymentRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
                                var freshPayment = await paymentRepo.GetByIdAsync(capturedPaymentId);
                                if (freshPayment is not null)
                                    await ((PaymentService)svc).SendPlainTextReceiptAsync(freshPayment, CancellationToken.None);
                            }
                            catch (Exception smsEx)
                            {
                                logger.LogError(smsEx, "SMS receipt fallback also failed for payment {PaymentId}.", capturedPaymentId);
                            }
                        }
                    }, CancellationToken.None);
                }
            }
        }
        else if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            payment.MarkFailed();
            payment.Activities.Add(PaymentActivity.Create(
                payment.Id, PaymentAction.Charge, PaymentStatus.Failed,
                payment.PaymentRef, payment.TotalAmount,
                gatewayMessage: "Chapa reported payment failed"));
        }

        payment.MarkWebhookReceived();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<PaymentStatusDto> SyncPaymentAsync(Guid paymentId, Guid patientId, CancellationToken ct = default)
    {
        var payment = await paymentRepository.GetByIdAsync(paymentId)
            ?? throw new NotFoundException(nameof(Payment), paymentId);

        if (payment.PatientId != patientId)
            throw new ForbiddenException("Access denied.");

        if (payment.Status is PaymentStatus.Completed or PaymentStatus.Failed or PaymentStatus.Refunded)
            return MapStatus(payment);

        // Retry up to 3 times (2 s apart) — Chapa SDK fires onPaymentFinished before
        // Chapa's backend settles the transaction, so verify may return "pending" initially.
        ChapaVerifyResponse? verify = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            verify = await chapaClient.VerifyPaymentAsync(payment.PaymentRef, ct);

            logger.LogInformation(
                "Chapa verify attempt {Attempt}/3 for payment {PaymentId} (txRef={TxRef}): " +
                "outerStatus={OuterStatus}, dataStatus={DataStatus}, flwRef={FlwRef}",
                attempt, paymentId, payment.PaymentRef,
                verify?.Status, verify?.Data?.Status, verify?.Data?.FlwRef);

            if (verify?.Data?.Status is not null &&
                !string.Equals(verify.Data.Status, "pending", StringComparison.OrdinalIgnoreCase))
                break;

            if (attempt < 3)
                await Task.Delay(2000, ct);
        }

        if (verify is null)
        {
            logger.LogWarning("Chapa verify returned null for payment {PaymentId}. Chapa API may be unreachable.", paymentId);
            return MapStatus(payment);
        }

        if (string.Equals(verify.Status, "success", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(verify.Data?.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            payment.Complete(verify.Data!.FlwRef);
            payment.Activities.Add(PaymentActivity.Create(
                payment.Id, PaymentAction.Charge, PaymentStatus.Completed,
                payment.PaymentRef, payment.TotalAmount,
                chapaTransactionId: verify.Data.FlwRef,
                paymentMethodRaw: verify.Data.PaymentType,
                confirmedAt: DateTime.UtcNow));

            if (payment.ReasonType == PaymentReasonType.Appointment
                && payment.Appointment != null
                && payment.Appointment.Status == AppointmentStatus.Pending)
            {
                payment.Appointment.Approve(Guid.Empty);
            }

            logger.LogInformation("Payment {PaymentId} marked Completed via manual sync.", paymentId);
        }
        else if (string.Equals(verify.Data?.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            payment.MarkFailed();
            payment.Activities.Add(PaymentActivity.Create(
                payment.Id, PaymentAction.Charge, PaymentStatus.Failed,
                payment.PaymentRef, payment.TotalAmount,
                gatewayMessage: "Chapa verify returned failed on manual sync"));

            logger.LogInformation("Payment {PaymentId} marked Failed via manual sync.", paymentId);
        }
        else
        {
            logger.LogWarning(
                "Payment {PaymentId} sync: no status change after retries. outerStatus={OuterStatus}, dataStatus={DataStatus}",
                paymentId, verify.Status, verify.Data?.Status);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return MapStatus(payment);
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
            p.ReceiptUrl,
            p.Patient?.FullName ?? string.Empty,
            p.Appointment?.Doctor?.FullName ?? string.Empty,
            p.PaymentMethod)).ToList();
    }

    public async Task<byte[]> GenerateReceiptAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await paymentRepository.GetByIdAsync(paymentId)
            ?? throw new NotFoundException(nameof(Payment), paymentId);
        if (payment.Status != PaymentStatus.Completed)
            throw new DomainException("Receipt can only be generated for completed payments.");
        if (payment.ReasonType == PaymentReasonType.Subscription)
            throw new DomainException("PDF receipts are not supported for subscription payments.");

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

    internal async Task SendPlainTextReceiptAsync(Payment payment, CancellationToken ct)
    {
        var msg =
            $"MediMind Payment Receipt\n" +
            $"Ref: {payment.PaymentRef}\n" +
            $"Amount: ETB {payment.Amount:F2}\n" +
            $"Date: {payment.PaymentDate:yyyy-MM-dd}\n" +
            $"Doctor: {payment.Appointment.Doctor.FullName}\n" +
            $"Status: PAID\n" +
            $"Transaction ID: {payment.ChapaTransactionId}";
        try
        {
            await notificationService.SendSmsAsync(payment.Patient.PhoneNumber, msg, ct);
        }
        catch (Exception smsEx)
        {
            logger.LogError(smsEx, "Plain-text SMS receipt also failed for payment {PaymentId}.", payment.Id);
        }
    }

    private static void EnsureReadAccess(Payment payment, string userType, Guid userId, Guid? centerId)
    {
        if (string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase) && payment.PatientId != userId)
            throw new ForbiddenException("Access denied.");
        if (string.Equals(userType, "Admin", StringComparison.OrdinalIgnoreCase) && payment.CenterId != centerId)
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
