using FluentValidation;
using MediatR;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;


namespace MediMind.Application.Features.Payments
{

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record PaymentDto(
    Guid Id,
    Guid AppointmentId,
    string PaymentRef,
    decimal Amount,
    string PaymentMethod,
    string Status,
    string? ChapaTransactionId,
    DateTime? PaymentDate);

// ═══════════════════════════════════════════════════════════════════════════════
// INITIATE PAYMENT (FR-026)
// ═══════════════════════════════════════════════════════════════════════════════

public record InitiatePaymentCommand(
    Guid AppointmentId,
    Guid PatientId,
    PaymentMethod PaymentMethod
) : IRequest<InitiatePaymentResult>;

public record InitiatePaymentResult(
    string PaymentRef,
    string? CheckoutUrl,
    decimal Amount);

public class InitiatePaymentHandler(
    IAppointmentRepository appointmentRepository,
    IPaymentRepository paymentRepository,
    MediMind.Domain.Common.Interfaces.IPaymentService paymentService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<InitiatePaymentCommand, InitiatePaymentResult>
{
    public async Task<InitiatePaymentResult> Handle(InitiatePaymentCommand request, CancellationToken ct)
    {
        var appointment = await appointmentRepository.GetByIdAsync(request.AppointmentId, ct)
                          ?? throw new NotFoundException(nameof(Appointment), request.AppointmentId);

        // Ensure patient owns this appointment
        if (appointment.PatientId != request.PatientId)
            throw new ForbiddenException("You can only pay for your own appointments.");

        if (appointment.Status != AppointmentStatus.Confirmed)
            throw new DomainException("Only confirmed appointments require payment.");

        // Check for duplicate payment initiation
        var existingPayments = await paymentRepository.GetByAppointmentAsync(request.AppointmentId, ct);
        if (existingPayments.Any(p => p.Status == PaymentStatus.Completed))
            throw new DomainException("Payment already completed for this appointment.");

        if (existingPayments.Any(p => p.Status == PaymentStatus.Pending))
            throw new DomainException("Payment already initiated for this appointment.");

        // Get consultation fee from doctor-center affiliation
        // (In real implementation, this would come from DoctorHealthcareCenter)
        var consultationFee = 500m; // Placeholder — load from DoctorHealthcareCenter

        var payment = Payment.Initiate(
            request.AppointmentId,
            request.PatientId,
            consultationFee,
            request.PaymentMethod);

        await paymentRepository.AddAsync(payment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Call Chapa gateway
        var chapaRequest = new ChapaPaymentRequest(
            TxRef: payment.PaymentRef,
            Amount: payment.Amount,
            Currency: "ETB",
            Email: appointment.Patient?.Email ?? string.Empty,
            FirstName: appointment.Patient?.FullName.Split(' ').FirstOrDefault() ?? string.Empty,
            LastName: appointment.Patient?.FullName.Split(' ').LastOrDefault() ?? string.Empty,
            PhoneNumber: appointment.Patient?.PhoneNumber ?? string.Empty,
            CallbackUrl: "https://api.medimind.et/api/v1/payments/webhook",
            ReturnUrl: "https://app.medimind.et/payment/success",
            Description: $"Appointment consultation fee");

        var chapaResponse = await paymentService.InitiateAsync(chapaRequest, ct);

        return new InitiatePaymentResult(payment.PaymentRef, chapaResponse.CheckoutUrl, payment.Amount);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// VERIFY PAYMENT WEBHOOK (FR-027) — called by Chapa
// ═══════════════════════════════════════════════════════════════════════════════

public record VerifyPaymentWebhookCommand(
    string Payload,
    string Signature,
    string TxRef,
    string ChapaTransactionId
) : IRequest<Unit>;

public class VerifyPaymentWebhookHandler(
    IPaymentRepository paymentRepository,
    MediMind.Domain.Common.Interfaces.IPaymentService paymentService,
    IPushNotificationService pushService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<VerifyPaymentWebhookCommand, Unit>
{
    public async Task<Unit> Handle(VerifyPaymentWebhookCommand request, CancellationToken ct)
    {
        // Security: verify HMAC-SHA256 signature (NFR-006 Payment Security)
        var isValid = await paymentService.VerifyWebhookAsync(request.Payload, request.Signature, ct);
        if (!isValid)
            throw new DomainException("Invalid webhook signature. Possible security breach.");

        var payment = await paymentRepository.GetByRefAsync(request.TxRef, ct)
                      ?? throw new NotFoundException("Payment", request.TxRef);

        payment.Complete(request.ChapaTransactionId);
        await unitOfWork.SaveChangesAsync(ct);

        // Notify patient of successful payment
        await pushService.SendToUserAsync(
            payment.PatientId,
            "Payment Successful ✅",
            $"Payment of ETB {payment.Amount:F2} confirmed. Your appointment is secured.",
            new Dictionary<string, string> { ["paymentRef"] = payment.PaymentRef },
            ct);

        return Unit.Value;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// GET PAYMENT RECEIPT (FR-028)
// ═══════════════════════════════════════════════════════════════════════════════

public record GetPaymentReceiptQuery(string PaymentRef, Guid PatientId) : IRequest<PaymentDto>;

public class GetPaymentReceiptHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentReceiptQuery, PaymentDto>
{
    public async Task<PaymentDto> Handle(GetPaymentReceiptQuery request, CancellationToken ct)
    {
        var payment = await paymentRepository.GetByRefAsync(request.PaymentRef, ct)
                      ?? throw new NotFoundException("Payment", request.PaymentRef);

        if (payment.PatientId != request.PatientId)
            throw new ForbiddenException("You can only view your own payment receipts.");

        return new PaymentDto(
            payment.Id,
            payment.AppointmentId,
            payment.PaymentRef,
            payment.Amount,
            payment.PaymentMethod.ToString(),
            payment.Status.ToString(),
            payment.ChapaTransactionId,
            payment.PaymentDate);
    }
}

}

namespace MediMind.Application.Features.Prescriptions
{

// ─── Prescription DTOs ─────────────────────────────────────────────────────────

    public record PrescriptionDto(
        Guid Id,
        Guid AppointmentId,
        string DoctorName,
        string PatientName,
        string CenterName,
        DateOnly IssueDate,
        DateOnly? ExpiryDate,
        string Diagnosis,
        List<MedicationItemDto> Medications,
        List<string> LabTests,
        string? FollowUpInstructions,
        string? SpecialInstructions,
        string? PrescriptionUrl,
        string? QrCode,
        string Status);

    public record MedicationItemDto(
        string Name,
        string Dosage,
        string Frequency,
        string Duration,
        string? Instructions = null);

// ═══════════════════════════════════════════════════════════════════════════════
// ISSUE PRESCRIPTION (Doctor only)
// ═══════════════════════════════════════════════════════════════════════════════

    public record IssuePrescriptionCommand(
        Guid AppointmentId,
        Guid DoctorId,
        string Diagnosis,
        List<MedicationItemDto> Medications,
        List<string>? LabTests,
        string? FollowUpInstructions,
        string? SpecialInstructions,
        DateOnly? ExpiryDate
    ) : IRequest<PrescriptionDto>;

    public class IssuePrescriptionValidator : AbstractValidator<IssuePrescriptionCommand>
    {
        public IssuePrescriptionValidator()
        {
            RuleFor(x => x.AppointmentId).NotEmpty();
            RuleFor(x => x.DoctorId).NotEmpty();
            RuleFor(x => x.Diagnosis).NotEmpty().MaximumLength(1000);
            RuleFor(x => x.Medications).NotEmpty().WithMessage("At least one medication is required.");
            RuleForEach(x => x.Medications).ChildRules(m =>
            {
                m.RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
                m.RuleFor(x => x.Dosage).NotEmpty().MaximumLength(100);
                m.RuleFor(x => x.Frequency).NotEmpty().MaximumLength(100);
                m.RuleFor(x => x.Duration).NotEmpty().MaximumLength(100);
                m.RuleFor(x => x.Instructions).MaximumLength(500).When(x => x.Instructions is not null);
            });
        }
    }

    public class IssuePrescriptionHandler(IPrescriptionService prescriptionService)
        : IRequestHandler<IssuePrescriptionCommand, PrescriptionDto>
    {
        public async Task<PrescriptionDto> Handle(IssuePrescriptionCommand request, CancellationToken ct)
        {
            var created = await prescriptionService.CreatePrescriptionAsync(
                new CreatePrescriptionDto(
                    request.AppointmentId,
                    request.Diagnosis,
                    request.Medications
                        .Select(m => new MedicationDto(
                            m.Name,
                            m.Dosage,
                            m.Frequency,
                            m.Duration,
                            m.Instructions))
                        .ToList(),
                    request.LabTests,
                    request.FollowUpInstructions,
                    request.SpecialInstructions,
                    request.ExpiryDate),
                request.DoctorId,
                ct);

            return new PrescriptionDto(
                created.PrescriptionId,
                created.AppointmentId,
                created.DoctorName,
                created.PatientName,
                created.CenterName,
                created.IssueDate,
                created.ExpiryDate,
                created.Diagnosis,
                created.Medications
                    .Select(m => new MedicationItemDto(m.Name, m.Dosage, m.Frequency, m.Duration, m.Instructions))
                    .ToList(),
                created.LabTests ?? [],
                created.FollowUpInstructions,
                created.SpecialInstructions,
                created.PdfUrl,
                created.QrCodeBase64,
                created.Status);
        }
    }
}
