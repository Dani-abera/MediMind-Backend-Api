using System.Text;
using MediMind.Application.Features.Payments;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentAppService = MediMind.Application.Features.Payments.IPaymentService;

namespace MediMind.API.Controllers;

/// <summary>
/// Chapa payment initiation, status, history, receipts, and webhook processing.
/// </summary>
[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController(PaymentAppService paymentService, ICurrentUser currentUser, ILogger<PaymentsController> logger) : ControllerBase
{
    /// <summary>Initiate a Chapa payment for an appointment (FR-090).</summary>
    [Tags("Patient — Payments")]
    [HttpPost("initiate")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(PaymentInitiationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentInitiationDto>> Initiate([FromBody] InitiatePaymentRequest request, CancellationToken ct)
    {
        var result = await paymentService.InitiatePaymentAsync(request.AppointmentId, currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Get the current payment status for a payment ID (FR-091).</summary>
    [Tags("Patient — Payments", "Admin — Payments")]
    [HttpGet("{id:guid}/status")]
    [Authorize(Policy = "PatientOrAdmin")]
    [ProducesResponseType(typeof(PaymentStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentStatusDto>> Status(Guid id, CancellationToken ct)
    {
        var result = await paymentService.GetPaymentStatusAsync(id, currentUser.UserType, currentUser.UserId, currentUser.TenantId, ct);
        return Ok(result);
    }

    /// <summary>Get the payment associated with a specific appointment (FR-091).</summary>
    [Tags("Patient — Payments", "Admin — Payments")]
    [HttpGet("appointment/{appointmentId:guid}")]
    [Authorize(Policy = "PatientOrAdmin")]
    [ProducesResponseType(typeof(PaymentStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentStatusDto>> ByAppointment(Guid appointmentId, CancellationToken ct)
    {
        var result = await paymentService.GetByAppointmentAsync(appointmentId, currentUser.UserType, currentUser.UserId, currentUser.TenantId, ct);
        return Ok(result);
    }

    /// <summary>List payment history for the current user or admin's center (FR-092).</summary>
    [Tags("Patient — Payments", "Admin — Payments")]
    [HttpGet]
    [Authorize(Policy = "PatientOrAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentHistoryItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentHistoryItemDto>>> History([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await paymentService.GetHistoryAsync(currentUser.UserType, currentUser.UserId, currentUser.TenantId, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Get the PDF receipt URL for a payment (FR-093). Generates the receipt if not yet stored.</summary>
    [Tags("Patient — Payments", "Admin — Payments")]
    [HttpGet("{id:guid}/receipt")]
    [Authorize(Policy = "PatientOrAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Receipt(Guid id, CancellationToken ct)
    {
        var status = await paymentService.GetPaymentStatusAsync(id, currentUser.UserType, currentUser.UserId, currentUser.TenantId, ct);
        if (string.IsNullOrEmpty(status.ReceiptUrl))
            await paymentService.GenerateReceiptAsync(status.PaymentId, ct);
        var updated = await paymentService.GetPaymentStatusAsync(id, currentUser.UserType, currentUser.UserId, currentUser.TenantId, ct);
        return Ok(new { url = updated.ReceiptUrl });
    }

    /// <summary>Chapa HMAC-verified webhook — updates payment and appointment status (FR-094).</summary>
    [Tags("System")]
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawPayload = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var signature = Request.Headers["Chapa-Signature"].FirstOrDefault() ?? string.Empty;
        try
        {
            await paymentService.ProcessWebhookAsync(rawPayload, signature, ct);
        }
        catch (Exception ex)
        {
            // Chapa retries non-200; always return 200 for idempotent webhook behavior.
            logger.LogError(ex, "Chapa webhook processing failed for payload length {Length}", rawPayload.Length);
        }

        return Ok(new { status = "received" });
    }

    /// <summary>Chapa payment redirect callback — acknowledges the browser return after checkout (FR-094).</summary>
    [Tags("System")]
    [HttpPost("callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Callback() => Ok(new { status = "callback_received" });
}

public sealed record InitiatePaymentRequest(Guid AppointmentId);
