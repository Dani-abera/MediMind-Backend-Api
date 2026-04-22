using MediMind.Application.Features.SuperAdmin;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers.SuperAdmin;

[ApiController]
[Route("api/v1/super-admin/doctors")]
[Authorize(Policy = "SuperAdminOnly")]
[Tags("SuperAdmin — Doctors")]
public class SuperAdminDoctorsController(
    ISuperAdminDoctorService doctorService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>List all doctors with optional filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SuperAdminDoctorSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? name,
        [FromQuery] string? specialization,
        [FromQuery] bool? licenseVerified,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await doctorService.GetAllDoctorsAsync(new SuperAdminDoctorQueryDto(name, specialization, licenseVerified, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>List doctors whose license has not been verified yet.</summary>
    [HttpGet("unverified")]
    [ProducesResponseType(typeof(IReadOnlyList<SuperAdminDoctorSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnverified(CancellationToken ct)
    {
        var result = await doctorService.GetUnverifiedDoctorsAsync(ct);
        return Ok(result);
    }

    /// <summary>Get a single doctor by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SuperAdminDoctorSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await doctorService.GetDoctorAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Mark a doctor's medical license as verified — unblocks login.</summary>
    [HttpPost("{id:guid}/verify-license")]
    [ProducesResponseType(typeof(SuperAdminDoctorSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyLicense(Guid id, [FromBody] VerifyDoctorLicenseDto dto, CancellationToken ct)
    {
        var result = await doctorService.VerifyLicenseAsync(id, dto, currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Revoke a doctor's license verification.</summary>
    [HttpPost("{id:guid}/unverify-license")]
    [ProducesResponseType(typeof(SuperAdminDoctorSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnverifyLicense(Guid id, [FromBody] UnverifyDoctorLicenseDto dto, CancellationToken ct)
    {
        var result = await doctorService.UnverifyLicenseAsync(id, dto, currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Suspend a doctor account.</summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] SuspendUserDto dto, CancellationToken ct)
    {
        await doctorService.SuspendDoctorAsync(id, dto.Reason, currentUser.UserId, ct);
        return NoContent();
    }

    /// <summary>Reactivate a suspended doctor account.</summary>
    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await doctorService.ReactivateDoctorAsync(id, currentUser.UserId, ct);
        return NoContent();
    }
}
