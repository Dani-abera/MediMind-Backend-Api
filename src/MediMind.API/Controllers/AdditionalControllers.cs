using MediatR;
using MediMind.Domain.Common.Interfaces;
using MediMind.Application.Features.Analytics;
using MediMind.Application.Features.HealthcareCenters;
using MediMind.Application.Features.Payments;
using MediMind.Application.Features.Prescriptions;
using MediMind.Application.Features.VideoConsultations;
using MediMind.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

// ═══════════════════════════════════════════════════════════════════════════════
// HEALTHCARE CENTERS CONTROLLER
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Healthcare center management — Registration, Doctors, Configuration</summary>
[Route("api/v1/healthcare-centers")]
public class HealthcareCentersController(IMediator mediator, ICurrentUser currentUser)
    : BaseController(mediator)
{
    /// <summary>
    /// Search available healthcare centers.
    /// No authentication required — patients browse before booking.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<HealthcareCenterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? city,
        [FromQuery] string? specialization,
        [FromQuery] string? search,
        [FromQuery] decimal? lat,
        [FromQuery] decimal? lng,
        CancellationToken ct)
    {
        var query = new SearchHealthcareCentersQuery(city, specialization, search, lat, lng);
        return Ok(await Mediator.Send(query, ct));
    }

    /// <summary>Get doctors available at a specific healthcare center.</summary>
    [HttpGet("{centerId}/doctors")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctors(
        Guid centerId, [FromQuery] string? specialization, CancellationToken ct)
    {
        var query = new GetDoctorsByCenterQuery(centerId, specialization);
        return Ok(await Mediator.Send(query, ct));
    }

    /// <summary>Register a new healthcare center (creates first admin account too).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterHealthcareCenterResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterHealthcareCenterCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return CreatedAtAction(nameof(Register), result);
    }

    /// <summary>Register a doctor at this center (Admin only).</summary>
    [HttpPost("{centerId}/doctors")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(RegisterDoctorResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterDoctor(
        Guid centerId, [FromBody] RegisterDoctorRequest request, CancellationToken ct)
    {
        if (currentUser.TenantId != centerId) return Forbid();

        var command = new RegisterDoctorCommand(
            centerId, currentUser.UserId,
            request.FullName, request.LicenseNumber, request.Specialization,
            request.Email, request.PhoneNumber, request.YearsOfExperience,
            request.ConsultationFee, request.Qualifications, request.LanguagesSpoken);

        var result = await Mediator.Send(command, ct);
        return CreatedAtAction(nameof(RegisterDoctor), result);
    }

    /// <summary>Configure a doctor's schedule at this center (Admin only — FR-021).</summary>
    [HttpPost("{centerId}/doctors/{doctorId}/schedule")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ConfigureSchedule(
        Guid centerId, Guid doctorId,
        [FromBody] ConfigureDoctorScheduleRequest request, CancellationToken ct)
    {
        if (currentUser.TenantId != centerId) return Forbid();

        await Mediator.Send(new ConfigureDoctorScheduleCommand(
            doctorId, centerId, currentUser.UserId,
            request.WorkingDays, request.StartTime, request.EndTime,
            request.SlotDuration, request.BreakStart, request.BreakEnd), ct);
        return NoContent();
    }

    /// <summary>Update center operational configuration (Admin only — FR-030).</summary>
    [HttpPatch("{centerId}/configuration")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateConfiguration(
        Guid centerId, [FromBody] UpdateCenterConfigurationRequest request, CancellationToken ct)
    {
        if (currentUser.TenantId != centerId) return Forbid();

        await Mediator.Send(new UpdateCenterConfigurationCommand(
            centerId, currentUser.UserId,
            request.SlotDurationMinutes, request.AdvanceBookingDays,
            request.CancellationHours, request.AutoApproveAppointments), ct);
        return NoContent();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// PAYMENTS CONTROLLER
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Payment processing via Chapa gateway</summary>
[Route("api/v1/payments")]
public class PaymentsController(IMediator mediator, ICurrentUser currentUser)
    : BaseController(mediator)
{
    /// <summary>Initiate payment for an appointment (Patient only — FR-026).</summary>
    [HttpPost("initiate")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(InitiatePaymentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> InitiatePayment(
        [FromBody] InitiatePaymentRequest request, CancellationToken ct)
    {
        var command = new InitiatePaymentCommand(
            request.AppointmentId,
            currentUser.UserId,
            request.PaymentMethod);
        return Ok(await Mediator.Send(command, ct));
    }

    /// <summary>
    /// Chapa payment webhook — verifies payment and confirms appointment.
    /// IMPORTANT: This endpoint must NOT require authentication (called by Chapa servers).
    /// Secured via HMAC-SHA256 signature verification (NFR-006).
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Webhook(
        [FromBody] ChapaWebhookPayload payload,
        [FromHeader(Name = "Chapa-Signature")] string signature,
        CancellationToken ct)
    {
        var rawPayload = System.Text.Json.JsonSerializer.Serialize(payload);

        await Mediator.Send(new VerifyPaymentWebhookCommand(
            rawPayload, signature, payload.TxRef, payload.ChapaTransactionId), ct);

        return Ok(new { status = "received" });
    }

    /// <summary>Get payment receipt (Patient only — FR-028).</summary>
    [HttpGet("{paymentRef}/receipt")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceipt(string paymentRef, CancellationToken ct)
    {
        var query = new GetPaymentReceiptQuery(paymentRef, currentUser.UserId);
        return Ok(await Mediator.Send(query, ct));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// VIDEO CONSULTATIONS CONTROLLER
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>WebRTC video consultation management</summary>
[Route("api/v1/consultations")]
[Authorize]
public class ConsultationsController(IMediator mediator, ICurrentUser currentUser)
    : BaseController(mediator)
{
    /// <summary>Doctor starts video consultation (creates WebRTC room — FR-017).</summary>
    [HttpPost("{appointmentId}/start")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(VideoConsultationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartConsultation(Guid appointmentId, CancellationToken ct)
    {
        var result = await Mediator.Send(
            new StartVideoConsultationCommand(appointmentId, currentUser.UserId), ct);
        return Ok(result);
    }

    /// <summary>Patient joins video consultation (gets WebRTC room ID — FR-017).</summary>
    [HttpPost("{appointmentId}/join")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(VideoConsultationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> JoinConsultation(Guid appointmentId, CancellationToken ct)
    {
        var result = await Mediator.Send(
            new JoinVideoConsultationCommand(appointmentId, currentUser.UserId), ct);
        return Ok(result);
    }

    /// <summary>Doctor ends video consultation.</summary>
    [HttpPost("{appointmentId}/end")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(VideoConsultationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> EndConsultation(
        Guid appointmentId, [FromQuery] string? videoQuality, CancellationToken ct)
    {
        var result = await Mediator.Send(
            new EndVideoConsultationCommand(appointmentId, currentUser.UserId, videoQuality), ct);
        return Ok(result);
    }

    /// <summary>Issue prescription after consultation (Doctor only).</summary>
    [HttpPost("{appointmentId}/prescriptions")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(PrescriptionDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> IssuePrescription(
        Guid appointmentId,
        [FromBody] IssuePrescriptionRequest request,
        CancellationToken ct)
    {
        var command = new IssuePrescriptionCommand(
            appointmentId,
            currentUser.UserId,
            request.Diagnosis,
            request.Medications,
            request.LabTests,
            request.FollowUpInstructions,
            request.SpecialInstructions,
            request.ExpiryDate);

        var result = await Mediator.Send(command, ct);
        return CreatedAtAction(nameof(IssuePrescription), result);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ANALYTICS CONTROLLER
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Analytics dashboard for healthcare center administrators (FR-024)</summary>
[Route("api/v1/analytics")]
[Authorize(Policy = "AdminOnly")]
public class AnalyticsController(IMediator mediator, ICurrentUser currentUser)
    : BaseController(mediator)
{
    /// <summary>Get today's dashboard analytics for a healthcare center.</summary>
    [HttpGet("{centerId}/dashboard")]
    [ProducesResponseType(typeof(AnalyticsDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(
        Guid centerId, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        if (currentUser.TenantId != centerId) return Forbid();

        var query = new GetDashboardAnalyticsQuery(
            centerId, date ?? DateOnly.FromDateTime(DateTime.UtcNow));
        return Ok(await Mediator.Send(query, ct));
    }

    /// <summary>Get weekly appointment trends (line chart data).</summary>
    [HttpGet("{centerId}/trends")]
    [ProducesResponseType(typeof(IReadOnlyList<WeeklyTrendDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWeeklyTrend(
        Guid centerId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken ct)
    {
        if (currentUser.TenantId != centerId) return Forbid();

        var query = new GetWeeklyTrendQuery(centerId, startDate, endDate);
        return Ok(await Mediator.Send(query, ct));
    }
}

// ─── Additional Request DTOs ──────────────────────────────────────────────────

public record RegisterDoctorRequest(
    string FullName, string LicenseNumber, string Specialization,
    string Email, string PhoneNumber, int YearsOfExperience,
    decimal ConsultationFee, string? Qualifications,
    List<string>? LanguagesSpoken);

public record ConfigureDoctorScheduleRequest(
    List<string> WorkingDays,
    TimeOnly StartTime, TimeOnly EndTime, int SlotDuration,
    TimeOnly? BreakStart, TimeOnly? BreakEnd);

public record UpdateCenterConfigurationRequest(
    int SlotDurationMinutes, int AdvanceBookingDays,
    int CancellationHours, bool AutoApproveAppointments);

public record InitiatePaymentRequest(Guid AppointmentId, PaymentMethod PaymentMethod);

public record ChapaWebhookPayload(
    string TxRef,
    string ChapaTransactionId,
    string Status,
    decimal Amount,
    string Currency);

public record IssuePrescriptionRequest(
    string Diagnosis,
    List<MedicationItemDto> Medications,
    List<string>? LabTests,
    string? FollowUpInstructions,
    string? SpecialInstructions,
    DateOnly? ExpiryDate);
