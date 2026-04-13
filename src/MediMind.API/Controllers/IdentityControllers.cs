using MediatR;
using MediMind.Application.Features.Auth;
using MediMind.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
public class AdminController(IMediator mediator) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] AdminRegisterCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(Register), new { adminId = id });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AdminLoginCommand command, CancellationToken ct)
    {
        var token = await mediator.Send(command, ct);
        return Ok(token);
    }

    [HttpPost("doctors")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateDoctor([FromBody] AdminCreateDoctorRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new AdminCreateDoctorCommand(
                request.FullName,
                request.PhoneNumber,
                request.Specialization,
                request.YearsOfExperience),
            ct);
        return CreatedAtAction(nameof(CreateDoctor), result);
    }
}

public record AdminCreateDoctorRequest(
    string FullName,
    string PhoneNumber,
    string Specialization,
    int YearsOfExperience);

[ApiController]
[Route("api/v1/auth")]
public class IdentityAuthController(IMediator mediator, IHostEnvironment environment) : ControllerBase
{
    [HttpPost("doctor/login")]
    [AllowAnonymous]
    public async Task<IActionResult> DoctorLogin([FromBody] DoctorLoginCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return Ok(new
        {
            message = "OTP sent to your registered phone number.",
            metadata = new
            {
                deliveryStatus = "sent",
                requestId = HttpContext.TraceIdentifier,
                environment = environment.EnvironmentName
            }
        });
    }

    [HttpPost("doctor/verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> DoctorVerifyOtp([FromBody] DoctorVerifyOtpCommand command, CancellationToken ct)
    {
        return Ok(await mediator.Send(command, ct));
    }

    [HttpPost("patient/request-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestPatientOtp([FromBody] PatientRequestOtpCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return Ok(new
        {
            message = "OTP sent successfully.",
            metadata = new
            {
                deliveryStatus = "sent",
                requestId = HttpContext.TraceIdentifier,
                environment = environment.EnvironmentName
            }
        });
    }

    [HttpPost("patient/verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyPatientOtp([FromBody] PatientVerifyOtpCommand command, CancellationToken ct)
    {
        return Ok(await mediator.Send(command, ct));
    }

    [HttpGet("patient/profile")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> GetPatientProfile(CancellationToken ct)
    {
        return Ok(await mediator.Send(new GetPatientProfileQuery(), ct));
    }

    [HttpPost("patient/profile")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> CreatePatientProfile([FromBody] PatientProfileUpsertRequest request, CancellationToken ct)
    {
        await mediator.Send(
            new UpsertPatientProfileCommand(
                request.FullName,
                request.DateOfBirth,
                PatientProfileRequestMapper.ParseGender(request.Gender),
                request.Address,
                request.MedicalHistory,
                request.Allergies,
                PatientProfileRequestMapper.ParseBloodTypeNullable(request.BloodType)),
            ct);
        return NoContent();
    }

    [HttpPut("patient/profile")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> UpdatePatientProfile([FromBody] PatientProfileUpsertRequest request, CancellationToken ct)
    {
        await mediator.Send(
            new UpsertPatientProfileCommand(
                request.FullName,
                request.DateOfBirth,
                PatientProfileRequestMapper.ParseGender(request.Gender),
                request.Address,
                request.MedicalHistory,
                request.Allergies,
                PatientProfileRequestMapper.ParseBloodTypeNullable(request.BloodType)),
            ct);
        return NoContent();
    }

    [HttpPatch("patient/profile")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> PatchPatientProfile([FromBody] PatientProfilePatchRequest request, CancellationToken ct)
    {
        await mediator.Send(
            new PatchPatientProfileCommand(
                request.FullName,
                request.DateOfBirth,
                PatientProfileRequestMapper.ParseGenderNullable(request.Gender),
                request.Address,
                request.MedicalHistory,
                request.Allergies,
                PatientProfileRequestMapper.ParseBloodTypeNullable(request.BloodType)),
            ct);
        return NoContent();
    }

    [HttpDelete("patient/profile")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> DeletePatientProfile(CancellationToken ct)
    {
        await mediator.Send(new DeletePatientProfileCommand(), ct);
        return NoContent();
    }
}

public record PatientProfileUpsertRequest(
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string Address,
    string? MedicalHistory,
    string? Allergies,
    string? BloodType);

public record PatientProfilePatchRequest(
    string? FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Address,
    string? MedicalHistory,
    string? Allergies,
    string? BloodType);

public static class PatientProfileRequestMapper
{
    public static Gender ParseGender(string value)
    {
        if (Enum.TryParse<Gender>(value, ignoreCase: true, out var gender))
            return gender;
        throw new ArgumentException("Invalid gender value. Use Male, Female, or Other.");
    }

    public static Gender? ParseGenderNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return ParseGender(value);
    }

    public static BloodType? ParseBloodTypeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (Enum.TryParse<BloodType>(value, ignoreCase: true, out var bloodType))
            return bloodType;
        throw new ArgumentException("Invalid bloodType value.");
    }
}
