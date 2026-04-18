using System.Text;
using MediMind.Application.Features.Payments;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentAppService = MediMind.Application.Features.Payments.IPaymentService;

namespace MediMind.API.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Tags("Payments")]
public sealed class PaymentsController(PaymentAppService paymentService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("initiate")]
    [Authorize(Policy = "PatientOnly")]
    public async Task<ActionResult<PaymentInitiationDto>> Initiate([FromBody] InitiatePaymentRequest request, CancellationToken ct)
    {
        var result = await paymentService.InitiatePaymentAsync(request.AppointmentId, currentUser.UserId, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/status")]
    [Authorize(Policy = "PatientOrAdmin")]
    public async Task<ActionResult<PaymentStatusDto>> Status(Guid id, CancellationToken ct)
    {
        var result = await paymentService.GetPaymentStatusAsync(id, currentUser.UserType, currentUser.UserId, currentUser.TenantId, ct);
        return Ok(result);
    }

    [HttpGet("appointment/{appointmentId:guid}")]
    [Authorize(Policy = "PatientOrAdmin")]
    public async Task<ActionResult<PaymentStatusDto>> ByAppointment(Guid appointmentId, CancellationToken ct)
    {
        var result = await paymentService.GetByAppointmentAsync(appointmentId, currentUser.UserType, currentUser.UserId, currentUser.TenantId, ct);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Policy = "PatientOrAdmin")]
    public async Task<ActionResult<IReadOnlyList<PaymentHistoryItemDto>>> History([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await paymentService.GetHistoryAsync(currentUser.UserType, currentUser.UserId, currentUser.TenantId, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/receipt")]
    [Authorize(Policy = "PatientOrAdmin")]
    public async Task<IActionResult> Receipt(Guid id, CancellationToken ct)
    {
        var status = await paymentService.GetPaymentStatusAsync(id, currentUser.UserType, currentUser.UserId, currentUser.TenantId, ct);
        var pdf = await paymentService.GenerateReceiptAsync(status.PaymentId, ct);
        return File(pdf, "application/pdf", $"receipt-{status.PaymentRef}.pdf");
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
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
        catch
        {
            // Chapa retries non-200; always return 200 for idempotent webhook behavior.
        }

        return Ok(new { status = "received" });
    }

    [HttpPost("callback")]
    [AllowAnonymous]
    public IActionResult Callback() => Ok(new { status = "callback_received" });
}

public sealed record InitiatePaymentRequest(Guid AppointmentId);
