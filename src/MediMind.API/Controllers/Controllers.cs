using MediMind.Application.Features.Auth;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MediMind.API.Controllers;

// ─── Base Controller ──────────────────────────────────────────────────────────

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class BaseController : ControllerBase { }

// ═══════════════════════════════════════════════════════════════════════════════
// AUTH CONTROLLER
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Authentication — Patient, Doctor, Admin, SuperAdmin, and shared token operations.</summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IPatientAuthService patientAuth,
    IDoctorAuthService doctorAuth,
    IAdminAuthService adminAuth,
    ISuperAdminAuthService superAdminAuth,
    ITokenService tokenService,
    IUserRepository userRepository,
    ICurrentUser currentUser) : ControllerBase
{
    // ── Patient ──────────────────────────────────────────────────────────────

    /// <summary>Register a new patient account (FR-001). Sends OTP to phone number.</summary>
    [Tags("Authentication")]
    [HttpPost("patient/register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> PatientRegister([FromBody] RegisterPatientRequest request, CancellationToken ct)
    {
        var result = await patientAuth.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(PatientRegister), result);
    }

    /// <summary>Request OTP for patient login (phone-based) (FR-002).</summary>
    [Tags("Authentication")]
    [HttpPost("patient/request-otp")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PatientRequestOtp([FromBody] PhoneRequest request, CancellationToken ct)
    {
        await patientAuth.RequestOtpAsync(request.PhoneNumber, ct);
        return Ok(new { message = "OTP sent successfully." });
    }

    /// <summary>Verify patient OTP and return JWT access + refresh tokens (FR-002).</summary>
    [Tags("Authentication")]
    [HttpPost("patient/verify-otp")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> PatientVerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken ct)
    {
        var result = await patientAuth.VerifyOtpAsync(request.PhoneNumber, request.OtpCode, ct);
        return Ok(result);
    }

    /// <summary>Get the authenticated patient's profile (FR-003).</summary>
    [Tags("Patient — Profile")]
    [HttpGet("patient/profile")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(PatientProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientProfile(CancellationToken ct) =>
        Ok(await patientAuth.GetProfileAsync(ct));

    /// <summary>Create or replace the authenticated patient's profile (FR-003).</summary>
    [Tags("Patient — Profile")]
    [HttpPost("patient/profile")]
    [HttpPut("patient/profile")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpsertPatientProfile([FromBody] UpsertPatientProfileApiRequest request, CancellationToken ct)
    {
        await patientAuth.UpsertProfileAsync(new UpsertPatientProfileRequest(
            request.FullName, request.DateOfBirth,
            ParseGender(request.Gender), request.Address,
            request.MedicalHistory, request.Allergies,
            ParseBloodType(request.BloodType)), ct);
        return NoContent();
    }

    /// <summary>Partially update the authenticated patient's profile fields (FR-003).</summary>
    [Tags("Patient — Profile")]
    [HttpPatch("patient/profile")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PatchPatientProfile([FromBody] PatchPatientProfileApiRequest request, CancellationToken ct)
    {
        await patientAuth.PatchProfileAsync(new PatchPatientProfileRequest(
            request.FullName, request.DateOfBirth,
            ParseGenderNullable(request.Gender), request.Address,
            request.MedicalHistory, request.Allergies,
            ParseBloodType(request.BloodType)), ct);
        return NoContent();
    }

    /// <summary>Permanently delete the authenticated patient's account (FR-003).</summary>
    [Tags("Patient — Profile")]
    [HttpDelete("patient/profile")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePatientProfile(CancellationToken ct)
    {
        await patientAuth.DeleteAsync(ct);
        return NoContent();
    }

    /// <summary>Change the authenticated patient's phone number after OTP verification.</summary>
    [Tags("Patient — Profile")]
    [HttpPost("patient/change-phone")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePhone([FromBody] ChangePhoneApiRequest request, CancellationToken ct)
    {
        await patientAuth.ChangePhoneAsync(
            new Application.Features.Auth.ChangePhoneRequest(request.NewPhoneNumber, request.OtpCode), ct);
        return NoContent();
    }

    // ── Doctor ───────────────────────────────────────────────────────────────

    /// <summary>Request OTP for doctor login using badge number (FR-004).</summary>
    [Tags("Authentication")]
    [HttpPost("doctor/request-otp")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DoctorRequestOtp([FromBody] BadgeRequest request, CancellationToken ct)
    {
        await doctorAuth.RequestOtpAsync(request.BadgeNumber, ct);
        return Ok(new { message = "OTP sent to your registered phone number." });
    }

    /// <summary>Verify doctor OTP and return JWT access + refresh tokens (FR-004).</summary>
    [Tags("Authentication")]
    [HttpPost("doctor/verify-otp")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DoctorVerifyOtp([FromBody] DoctorVerifyOtpApiRequest request, CancellationToken ct)
    {
        var result = await doctorAuth.VerifyOtpAsync(request.BadgeNumber, request.OtpCode, ct);
        return Ok(result);
    }

    // ── Admin ────────────────────────────────────────────────────────────────

    /// <summary>Register a new healthcare center admin account (FR-005).</summary>
    [Tags("Authentication")]
    [HttpPost("admin/register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AdminRegister([FromBody] AdminRegisterRequest request, CancellationToken ct)
    {
        var id = await adminAuth.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(AdminRegister), new { adminId = id });
    }

    /// <summary>Admin login with email and password (FR-005).</summary>
    [Tags("Authentication")]
    [HttpPost("admin/login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminLogin([FromBody] AdminLoginApiRequest request, CancellationToken ct)
    {
        var result = await adminAuth.LoginAsync(request.Email, request.Password, ct);
        return Ok(result);
    }

    // ── SuperAdmin ───────────────────────────────────────────────────────────

    /// <summary>SuperAdmin login with email and password. Returns a JWT with <c>user_type = SuperAdmin</c>.</summary>
    /// <remarks>
    /// Use this endpoint to authenticate as the platform SuperAdmin.
    ///
    /// Default seed credentials (set in `appsettings.json`):
    /// - **Email:** `superadmin@medimind.et`
    /// - **Password:** `MediMind@2025!`
    ///
    /// After 5 consecutive failures the account is locked for **15 minutes**.
    /// The returned `accessToken` is valid for 15 minutes; use `/auth/refresh-token` to extend the session.
    /// </remarks>
    [Tags("Authentication")]
    [HttpPost("superadmin/login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SuperAdminLogin([FromBody] AdminLoginApiRequest request, CancellationToken ct)
    {
        var result = await superAdminAuth.LoginAsync(request.Email, request.Password, ct);
        return Ok(result);
    }

    /// <summary>Admin creates a doctor account linked to their healthcare center (FR-006).</summary>
    [Tags("Admin — Doctors")]
    [HttpPost("admin/doctors")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(DoctorCreatedResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> AdminCreateDoctor([FromBody] CreateDoctorRequest request, CancellationToken ct)
    {
        var result = await adminAuth.CreateDoctorAsync(request, ct);
        return CreatedAtAction(nameof(AdminCreateDoctor), result);
    }

    // ── Shared ───────────────────────────────────────────────────────────────

    /// <summary>Exchange a valid refresh token for a new access + refresh token pair.</summary>
    [Tags("Authentication")]
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenApiRequest request, CancellationToken ct)
    {
        if (!tokenService.ValidateRefreshToken(request.RefreshToken, out var userId))
            throw new Domain.Exceptions.DomainException("Session expired. Please login.");

        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new Domain.Exceptions.NotFoundException(nameof(Domain.Entities.User), userId);

        var tenantId = (user as Domain.Entities.HealthcareCenterAdmin)?.CenterId;
        var (accessToken, refreshToken) = tokenService.GenerateTokens(user.Id, user.UserType.ToString(), tenantId);
        return Ok(new AuthResult(accessToken, refreshToken, user.Id, user.UserType.ToString(), user.FullName));
    }

    /// <summary>Logout and revoke the current user's refresh token.</summary>
    [Tags("Authentication")]
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        tokenService.RevokeRefreshToken(currentUser.UserId);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Domain.Enums.Gender ParseGender(string value)
    {
        if (Enum.TryParse<Domain.Enums.Gender>(value, ignoreCase: true, out var g)) return g;
        throw new Domain.Exceptions.DomainException("Invalid gender. Use Male, Female, or Other.");
    }

    private static Domain.Enums.Gender? ParseGenderNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseGender(value);

    private static Domain.Enums.BloodType? ParseBloodType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Enum.TryParse<Domain.Enums.BloodType>(value, ignoreCase: true, out var b)) return b;
        throw new Domain.Exceptions.DomainException("Invalid blood type.");
    }
}

// ─── API Request Records (string-typed for JSON deserialization) ──────────────

public record PhoneRequest(string PhoneNumber);
public record BadgeRequest(string BadgeNumber);
public record VerifyOtpRequest(string PhoneNumber, string OtpCode);
public record DoctorVerifyOtpApiRequest(string BadgeNumber, string OtpCode);
public record AdminLoginApiRequest(string Email, string Password);
public record RefreshTokenApiRequest(string RefreshToken);

public record ChangePhoneApiRequest(string NewPhoneNumber, string OtpCode);

public record UpsertPatientProfileApiRequest(
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string Address,
    string? MedicalHistory,
    string? Allergies,
    string? BloodType);

public record PatchPatientProfileApiRequest(
    string? FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Address,
    string? MedicalHistory,
    string? Allergies,
    string? BloodType);


// ═══════════════════════════════════════════════════════════════════════════════
// ERROR CONTROLLER
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Global error handler — maps domain exceptions to RFC 7807 ProblemDetails responses.</summary>
[ApiExplorerSettings(IgnoreApi = true)]
[Route("/error")]
public class ErrorController : ControllerBase
{
    [HttpGet, HttpPost, HttpPut, HttpDelete, HttpPatch]
    public IActionResult HandleError()
    {
        var feature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (feature?.Error is null)
            return Problem(statusCode: 500, title: "Internal Server Error", instance: HttpContext.Request.Path);

        // Validation errors — return structured "errors" map per RFC 7807 + FluentValidation conventions
        if (feature.Error is FluentValidation.ValidationException ve)
        {
            var modelState = new ModelStateDictionary();
            foreach (var err in ve.Errors)
                modelState.AddModelError(err.PropertyName, err.ErrorMessage);
            return ValidationProblem(
                modelStateDictionary: modelState,
                instance: HttpContext.Request.Path);
        }

        var (status, title) = feature.Error switch
        {
            Domain.Exceptions.DomainException => (400, "Business Rule Violation"),
            Domain.Exceptions.NotFoundException => (404, "Resource Not Found"),
            Domain.Exceptions.ForbiddenException => (403, "Access Denied"),
            Domain.Exceptions.TenantIsolationException => (403, "Tenant Isolation Violation"),
            _ => (500, "Internal Server Error")
        };

        return Problem(
            statusCode: status,
            title: title,
            detail: feature.Error.Message,
            instance: HttpContext.Request.Path,
            type: "https://tools.ietf.org/html/rfc7807");
    }
}

