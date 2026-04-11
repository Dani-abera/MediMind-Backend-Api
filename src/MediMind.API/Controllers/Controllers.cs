using MediatR;
using MediMind.Application.Features.Auth;
using MediMind.Application.Features.Appointments;
using MediMind.Application.Features.HealthRecords;
using MediMind.Application.Features.Queue;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MediMind.Domain.Enums;

namespace MediMind.API.Controllers;

// ─── Base Controller ──────────────────────────────────────────────────────────

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class BaseController(IMediator mediator) : ControllerBase
{
    protected readonly IMediator Mediator = mediator;
}

// ═══════════════════════════════════════════════════════════════════════════════
// AUTH CONTROLLER
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Authentication — Registration, OTP, Login, Token refresh</summary>
[Tags("Authentication")]
[Route("api/v1/auth")]
public class AuthController(IMediator mediator) : BaseController(mediator)
{
    /// <summary>Register a new patient account. Sends OTP to phone number.</summary>
    [HttpPost("register/patient")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterPatient(
        [FromBody] RegisterPatientCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return CreatedAtAction(nameof(RegisterPatient), result);
    }

    /// <summary>Verify OTP code — activates account and returns JWT tokens.</summary>
    [HttpPost("verify-otp")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Send OTP to phone number for patient login.</summary>
    [HttpPost("login/patient/send-otp")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendPatientLoginOtp(
        [FromBody] SendLoginOtpCommand command, CancellationToken ct)
    {
        await Mediator.Send(command, ct);
        return Ok(new { message = "OTP sent to your registered phone number." });
    }

    /// <summary>Send OTP to doctor using their badge/license number.</summary>
    [HttpPost("login/doctor/send-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> SendDoctorLoginOtp(
        [FromBody] SendDoctorLoginOtpCommand command, CancellationToken ct)
    {
        await Mediator.Send(command, ct);
        return Ok(new { message = "OTP sent to your registered phone number." });
    }

    /// <summary>Refresh access token using refresh token.</summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return Ok(result);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// APPOINTMENTS CONTROLLER
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Appointment management — Book, Approve, Reject, Cancel, Query</summary>
[Tags("Appointments")]
[Authorize]
public class AppointmentsController(IMediator mediator, ICurrentUser currentUser)
    : BaseController(mediator)
{
    /// <summary>
    /// Get patient's appointment history.
    /// Patients can only see their own appointments (RBAC enforced).
    /// </summary>
    [HttpGet("my")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAppointments(CancellationToken ct)
    {
        var query = new GetPatientAppointmentsQuery(currentUser.UserId);
        return Ok(await Mediator.Send(query, ct));
    }

    /// <summary>Get available time slots for a doctor on a specific date.</summary>
    [HttpGet("doctors/{doctorId}/slots")]
    [ProducesResponseType(typeof(IReadOnlyList<TimeOnly>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableSlots(
        Guid doctorId,
        [FromQuery] Guid centerId,
        [FromQuery] DateOnly date,
        CancellationToken ct)
    {
        var query = new GetDoctorAvailableSlotsQuery(doctorId, centerId, date);
        return Ok(await Mediator.Send(query, ct));
    }

    /// <summary>Book an appointment. Patient can only book for themselves.</summary>
    [HttpPost]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(BookAppointmentResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BookAppointment(
        [FromBody] BookAppointmentRequest request, CancellationToken ct)
    {
        var command = new BookAppointmentCommand(
            currentUser.UserId,  // Patient ID from JWT — cannot book for others
            request.DoctorId,
            request.CenterId,
            request.AppointmentDate,
            request.AppointmentTime,
            request.ReasonForVisit,
            request.Symptoms);

        var result = await Mediator.Send(command, ct);
        return CreatedAtAction(nameof(BookAppointment), result);
    }

    /// <summary>Approve a pending appointment (Healthcare Center Admin only).</summary>
    [HttpPatch("{appointmentId}/approve")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ApproveAppointment(Guid appointmentId, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId
            ?? throw new UnauthorizedAccessException("Admin must be associated with a healthcare center.");

        await Mediator.Send(new ApproveAppointmentCommand(appointmentId, currentUser.UserId, tenantId), ct);
        return NoContent();
    }

    /// <summary>Reject a pending appointment (Healthcare Center Admin only).</summary>
    [HttpPatch("{appointmentId}/reject")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RejectAppointment(
        Guid appointmentId, [FromBody] RejectRequest request, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId
            ?? throw new UnauthorizedAccessException("Admin must be associated with a healthcare center.");

        await Mediator.Send(new RejectAppointmentCommand(
            appointmentId, currentUser.UserId, tenantId, request.Reason), ct);
        return NoContent();
    }

    /// <summary>Cancel an appointment (Patient or Admin).</summary>
    [HttpPatch("{appointmentId}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CancelAppointment(
        Guid appointmentId, [FromBody] CancelRequest request, CancellationToken ct)
    {
        await Mediator.Send(new CancelAppointmentCommand(
            appointmentId, currentUser.UserId, request.Reason, currentUser.TenantId), ct);
        return NoContent();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// QUEUE CONTROLLER
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Real-time queue management for healthcare centers</summary>
[Tags("Queue")]
[Authorize]
public class QueueController(IMediator mediator, ICurrentUser currentUser)
    : BaseController(mediator)
{
    /// <summary>Get the full queue dashboard for a healthcare center (Admin only).</summary>
    [HttpGet("centers/{centerId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IReadOnlyList<QueueEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCenterQueue(
        Guid centerId, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        // Tenant isolation: admin can only see their own center's queue
        if (currentUser.TenantId != centerId)
            return Forbid();

        var query = new GetCenterQueueQuery(centerId, date ?? DateOnly.FromDateTime(DateTime.UtcNow));
        return Ok(await Mediator.Send(query, ct));
    }

    /// <summary>Call next patient in queue (Admin action — triggers real-time SignalR broadcast).</summary>
    [HttpPost("centers/{centerId}/call-next")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(QueueEntryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CallNextPatient(Guid centerId, CancellationToken ct)
    {
        if (currentUser.TenantId != centerId) return Forbid();

        var result = await Mediator.Send(new CallNextPatientCommand(centerId, currentUser.UserId), ct);
        return Ok(result);
    }

    /// <summary>Mark a patient as no-show (Admin only).</summary>
    [HttpPatch("{queueEntryId}/no-show")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> MarkNoShow(Guid queueEntryId, CancellationToken ct)
    {
        var centerId = currentUser.TenantId
            ?? throw new UnauthorizedAccessException("Admin must be associated with a healthcare center.");

        await Mediator.Send(new MarkNoShowCommand(queueEntryId, centerId), ct);
        return NoContent();
    }

    /// <summary>Get patient's own queue status (Patient only) — polling fallback when WebSocket unavailable.</summary>
    [HttpGet("my-status/{appointmentId}")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(PatientQueueStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyQueueStatus(Guid appointmentId, CancellationToken ct)
    {
        var query = new GetPatientQueueStatusQuery(appointmentId, currentUser.UserId);
        return Ok(await Mediator.Send(query, ct));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// HEALTH RECORDS CONTROLLER
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Patient health monitoring — log vitals and request AI predictions</summary>
[Tags("Health records")]
[Authorize]
[Route("api/v1/health")]
public class HealthController(IMediator mediator, ICurrentUser currentUser)
    : BaseController(mediator)
{
    /// <summary>Log daily vital signs. Patients can only log their own data.</summary>
    [HttpPost("records")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(HealthRecordDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LogHealthData(
        [FromBody] LogHealthDataRequest request, CancellationToken ct)
    {
        var command = new LogHealthDataCommand(
            currentUser.UserId,
            request.RecordDate,
            request.RecordTime ?? TimeOnly.FromDateTime(DateTime.UtcNow),
            request.SystolicBp,
            request.DiastolicBp,
            request.GlucoseLevel,
            request.Weight,
            request.Height,
            request.Temperature,
            request.HeartRate,
            request.OxygenSaturation,
            request.RespiratoryRate,
            request.Notes);

        var result = await Mediator.Send(command, ct);
        return CreatedAtAction(nameof(LogHealthData), result);
    }

    /// <summary>Get health records history (last N days).</summary>
    [HttpGet("records")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(IReadOnlyList<HealthRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHealthRecords(
        [FromQuery] int days = 30, CancellationToken ct = default)
    {
        var query = new GetHealthRecordsQuery(currentUser.UserId, days);
        return Ok(await Mediator.Send(query, ct));
    }

    /// <summary>
    /// Request AI disease risk prediction.
    /// Calls Python Flask ML microservice with patient's health data.
    /// Minimum 7 days of data required; 30+ days for high confidence.
    /// </summary>
    [HttpPost("predictions/request")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(HealthPredictionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestPrediction(CancellationToken ct)
    {
        var result = await Mediator.Send(new RequestPredictionCommand(currentUser.UserId), ct);
        return Ok(result);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ERROR CONTROLLER
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Global error handler — maps domain exceptions to HTTP responses.</summary>
[ApiExplorerSettings(IgnoreApi = true)]
[Route("/error")]
public class ErrorController : ControllerBase
{
    [HttpGet, HttpPost, HttpPut, HttpDelete, HttpPatch]
    public IActionResult HandleError()
    {
        var feature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (feature?.Error is null) return Problem();

        var (status, title) = feature.Error switch
        {
            Domain.Exceptions.DomainException => (400, "Business Rule Violation"),
            Domain.Exceptions.NotFoundException => (404, "Resource Not Found"),
            Domain.Exceptions.ForbiddenException => (403, "Access Denied"),
            Domain.Exceptions.TenantIsolationException => (403, "Tenant Isolation Violation"),
            FluentValidation.ValidationException => (400, "Validation Failed"),
            _ => (500, "Internal Server Error")
        };

        return Problem(
            statusCode: status,
            title: title,
            detail: feature.Error.Message);
    }
}

// ─── Request DTOs (API layer only — keeps commands clean) ─────────────────────

public record BookAppointmentRequest(
    Guid DoctorId,
    Guid CenterId,
    DateOnly AppointmentDate,
    TimeOnly AppointmentTime,
    string ReasonForVisit,
    string? Symptoms);

public record RejectRequest(string Reason);
public record CancelRequest(string Reason);

public record LogHealthDataRequest(
    DateOnly RecordDate,
    TimeOnly? RecordTime,
    int? SystolicBp,
    int? DiastolicBp,
    decimal? GlucoseLevel,
    decimal? Weight,
    decimal? Height,
    decimal? Temperature,
    int? HeartRate,
    int? OxygenSaturation,
    int? RespiratoryRate,
    string? Notes);
