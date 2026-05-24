using FluentValidation;
using MediMind.Application.Auth;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MediMind.Application.Features.Auth;

/// <summary>
/// Normalizes Ethiopian phone numbers to 251XXXXXXXXX (no + prefix) for consistent DB storage
/// and Geez SMS delivery. Application layer cannot reference Infrastructure, so the logic is
/// duplicated here intentionally.
/// </summary>
internal static class PhoneNormalizer
{
    internal static string Normalize(string phone)
    {
        var p = phone.Trim().Replace(" ", "").Replace("-", "");
        if (p.StartsWith('+')) p = p[1..];
        if (p.StartsWith("09") && p.Length == 10) return "251" + p[1..];
        if (p.StartsWith("07") && p.Length == 10) return "251" + p[1..];
        if (p.StartsWith('9') && p.Length == 9) return "251" + p;
        if (p.StartsWith('7') && p.Length == 9) return "251" + p;
        return p; // already 251XXXXXXXXX
    }
}

// ─── Constants ────────────────────────────────────────────────────────────────

public static class OtpPurposes
{
    public const string DoctorLogin = "doctor_login";
    public const string PatientLogin = "patient_login";
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record AuthResult(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string UserType,
    string FullName,
    bool IsProfileComplete);

public record RegisterResult(Guid UserId, string Message);
public record DoctorCreatedResult(Guid DoctorId, string BadgeNumber, string Message);
public record DoctorInvitationResult(Guid InvitationId, string Message);
public record PendingInvitationDto(Guid InvitationId, string FullName, string Email, string PhoneNumber, string Specialization, string LicenseNumber, DateTime ExpiresAt);
public record AcceptInvitationRequest(string Token, string Password);
public record AcceptInvitationResult(Guid DoctorId, string BadgeNumber, string Message);

public record PatientProfileDto(
    Guid PatientId,
    string PhoneNumber,
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? Address,
    string? MedicalHistory,
    string? Allergies,
    BloodType? BloodType);

// ─── Request Records ──────────────────────────────────────────────────────────

public record RegisterPatientRequest(
    string Email,
    string PhoneNumber,
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    string Password);

public record UpsertPatientProfileRequest(
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    string Address,
    string? MedicalHistory,
    string? Allergies,
    BloodType? BloodType);

public record PatchPatientProfileRequest(
    string? FullName,
    DateOnly? DateOfBirth,
    Gender? Gender,
    string? Address,
    string? MedicalHistory,
    string? Allergies,
    BloodType? BloodType);

public record AdminRegisterRequest(string FullName, string Email, string PhoneNumber, string Password);
public record CreateDoctorRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    string Specialization,
    string LicenseNumber,
    int YearsOfExperience,
    decimal ConsultationFee);
public record InviteDoctorRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    string Specialization,
    string LicenseNumber,
    int YearsOfExperience,
    decimal ConsultationFee);

// ─── Validators ───────────────────────────────────────────────────────────────

public class RegisterPatientRequestValidator : AbstractValidator<RegisterPatientRequest>
{
    public RegisterPatientRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PhoneNumber).NotEmpty()
            .Matches(@"^\+?251[0-9]{9}$")
            .WithMessage("Phone number must be in Ethiopian format: 251XXXXXXXXX or +251XXXXXXXXX");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DateOfBirth)
            .Must(dob => dob <= DateOnly.FromDateTime(DateTime.Today.AddYears(-18)))
            .WithMessage("Patient must be at least 18 years old.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.");
    }
}

public class AdminRegisterRequestValidator : AbstractValidator<AdminRegisterRequest>
{
    public AdminRegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PhoneNumber).NotEmpty()
            .Matches(@"^\+?251[0-9]{9}$")
            .WithMessage("Phone number must be in Ethiopian format: 251XXXXXXXXX or +251XXXXXXXXX");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public class CreateDoctorRequestValidator : AbstractValidator<CreateDoctorRequest>
{
    public CreateDoctorRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress()
            .WithMessage("Please enter a valid email address");
        RuleFor(x => x.PhoneNumber).NotEmpty()
            .Matches(@"^\+?251[0-9]{9}$")
            .WithMessage("Phone number must be in Ethiopian format: 251XXXXXXXXX or +251XXXXXXXXX");
        RuleFor(x => x.Specialization).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LicenseNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.YearsOfExperience).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ConsultationFee).GreaterThan(0)
            .WithMessage("Consultation fee must be greater than 0 ETB.");
    }
}

public class InviteDoctorRequestValidator : AbstractValidator<InviteDoctorRequest>
{
    public InviteDoctorRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress()
            .WithMessage("Please enter valid email address");
        RuleFor(x => x.PhoneNumber).NotEmpty()
            .Matches(@"^\+?251[0-9]{9}$")
            .WithMessage("Phone number must be in Ethiopian format: 251XXXXXXXXX or +251XXXXXXXXX");
        RuleFor(x => x.Specialization).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LicenseNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.YearsOfExperience).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ConsultationFee).GreaterThan(0)
            .WithMessage("Consultation fee must be greater than 0.");
    }
}

// ─── Service Interfaces ───────────────────────────────────────────────────────

public record ChangePhoneRequest(string NewPhoneNumber, string OtpCode);

public interface IPatientAuthService
{
    Task<RegisterResult> RegisterAsync(RegisterPatientRequest request, CancellationToken ct);
    Task RequestOtpAsync(string phoneNumber, CancellationToken ct);
    Task<AuthResult> VerifyOtpAsync(string phoneNumber, string otpCode, CancellationToken ct);
    Task<PatientProfileDto> GetProfileAsync(CancellationToken ct);
    Task UpsertProfileAsync(UpsertPatientProfileRequest request, CancellationToken ct);
    Task PatchProfileAsync(PatchPatientProfileRequest request, CancellationToken ct);
    Task DeleteAsync(CancellationToken ct);
    Task ChangePhoneAsync(ChangePhoneRequest request, CancellationToken ct);
}

public interface IDoctorAuthService
{
    Task RequestOtpAsync(string badgeNumber, CancellationToken ct);
    Task<AuthResult> VerifyOtpAsync(string badgeNumber, string otpCode, CancellationToken ct);
}

public interface IAdminAuthService
{
    Task<Guid> RegisterAsync(AdminRegisterRequest request, CancellationToken ct);
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct);
    Task<DoctorCreatedResult> CreateDoctorAsync(CreateDoctorRequest request, CancellationToken ct);
    Task<DoctorInvitationResult> InviteDoctorAsync(Guid centerId, InviteDoctorRequest request, CancellationToken ct);
    Task<AcceptInvitationResult> AcceptDoctorInvitationAsync(AcceptInvitationRequest request, CancellationToken ct);
    Task<IReadOnlyList<PendingInvitationDto>> GetPendingInvitationsAsync(Guid centerId, CancellationToken ct);
}

public interface ISuperAdminAuthService
{
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct);
}

// ─── Patient Auth Service ─────────────────────────────────────────────────────

public class PatientAuthService(
    IUserRepository userRepository,
    IPatientRepository patientRepository,
    IOtpVerificationRepository otpVerificationRepository,
    IPasswordService passwordService,
    IOtpService otpService,
    ISmsService smsService,
    IAuthService authService,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IOptions<TestOtpOptions> testOtpOptions,
    IHostEnvironment hostEnvironment) : IPatientAuthService
{
    public async Task<RegisterResult> RegisterAsync(RegisterPatientRequest request, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(request.PhoneNumber);

        if (await userRepository.ExistsByPhoneForRoleAsync(phone, UserType.Patient, ct))
            throw new DomainException("A patient account with this phone number already exists.");

        var patient = new Patient(request.Email, phone, request.FullName, request.DateOfBirth, request.Gender);
        patient.SetPasswordHash(passwordService.HashPassword(request.Password));

        await patientRepository.AddAsync(patient, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new RegisterResult(patient.Id, "Account created. Verify your phone number via OTP to activate.");
    }

    public async Task RequestOtpAsync(string phoneNumber, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(phoneNumber);

        if (testOtpOptions.Value.Enabled && !hostEnvironment.IsProduction())
            return;

        var otp = otpService.GenerateOtp();
        await otpVerificationRepository.AddAsync(new OtpVerification(phone, otp, OtpPurposes.PatientLogin), ct);
        await unitOfWork.SaveChangesAsync(ct);
        await smsService.SendOtpAsync(phone, otp, ct);
    }

    public async Task<AuthResult> VerifyOtpAsync(string phoneNumber, string otpCode, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(phoneNumber);
        var useTestOtp = IsTestOtp(otpCode);
        OtpVerification? latestOtp = null;

        if (!useTestOtp)
        {
            latestOtp = await otpVerificationRepository.GetLatestActiveAsync(phone, OtpPurposes.PatientLogin, ct)
                ?? throw new DomainException("OTP not found or expired.");
            if (!latestOtp.Matches(otpCode))
                throw new DomainException("Invalid or expired OTP.");
        }

        // Query Db.Patients directly — avoids EF Core TPT INNER JOIN issues with ghost rows
        // (orphan users rows that have no matching patients row from past data corruption).
        var existing = await patientRepository.GetByPhoneAsync(phone, ct);

        Patient patient;
        if (existing != null)
        {
            patient = existing;
        }
        else
        {
            // First-time login: auto-create a minimal patient account.
            // Use a UUID-suffixed email as fallback if the phone-based email is taken
            // (can happen when a ghost row exists in users without a patients row).
            var autoEmail = $"{phone}@patient.medimind.local";
            if (await userRepository.ExistsByEmailAsync(autoEmail, ct))
                autoEmail = $"{phone}.{Guid.NewGuid():N}@patient.medimind.local";

            patient = new Patient(
                autoEmail,
                phone,
                "Patient",
                DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
                Gender.Other);
            patient.SetPasswordHash(passwordService.HashPassword(Guid.NewGuid().ToString("N")));
            await patientRepository.AddAsync(patient, ct);
        }

        latestOtp?.MarkUsed();
        if (patient.Status == UserStatus.Pending)
            patient.Activate();
        patient.RecordLogin();

        bool isProfileComplete = patient.FullName != "Patient";
        var tokens = await authService.GenerateAuthTokensAsync(patient, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new AuthResult(tokens.AccessToken, tokens.RefreshToken, patient.Id, patient.UserType.ToString(), patient.FullName, isProfileComplete);
    }

    public async Task<PatientProfileDto> GetProfileAsync(CancellationToken ct)
    {
        var patient = await patientRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(Patient), currentUser.UserId);
        return new PatientProfileDto(patient.Id, patient.PhoneNumber, patient.FullName, patient.DateOfBirth,
            patient.Gender, patient.Address, patient.MedicalHistory, patient.Allergies, patient.BloodType);
    }

    public async Task UpsertProfileAsync(UpsertPatientProfileRequest request, CancellationToken ct)
    {
        var patient = await patientRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(Patient), currentUser.UserId);
        patient.UpdateIdentityProfile(request.FullName, request.DateOfBirth, request.Gender);
        patient.UpdateMedicalProfile(request.Address, request.BloodType, request.Allergies, null, null, request.MedicalHistory, null, null);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task PatchProfileAsync(PatchPatientProfileRequest request, CancellationToken ct)
    {
        var patient = await patientRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(Patient), currentUser.UserId);
        patient.UpdateIdentityProfile(
            request.FullName ?? patient.FullName,
            request.DateOfBirth ?? patient.DateOfBirth,
            request.Gender ?? patient.Gender);
        patient.UpdateMedicalProfile(
            request.Address ?? patient.Address,
            request.BloodType ?? patient.BloodType,
            request.Allergies ?? patient.Allergies,
            patient.EmergencyContactName,
            patient.EmergencyContactPhone,
            request.MedicalHistory ?? patient.MedicalHistory,
            patient.ChronicConditions,
            patient.CurrentMedications);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CancellationToken ct)
    {
        var patient = await patientRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(Patient), currentUser.UserId);
        await patientRepository.DeleteAsync(patient, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ChangePhoneAsync(ChangePhoneRequest request, CancellationToken ct)
    {
        var newPhone = PhoneNormalizer.Normalize(request.NewPhoneNumber);

        var user = await userRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), currentUser.UserId);

        if (!IsTestOtp(request.OtpCode))
        {
            var otp = await otpVerificationRepository.GetLatestActiveAsync(newPhone, OtpPurposes.PatientLogin, ct);
            if (otp is null || !otp.Matches(request.OtpCode))
                throw new DomainException("Invalid or expired OTP.");
            otp.MarkUsed();
        }

        if (await userRepository.ExistsByPhoneForRoleAsync(newPhone, user.UserType, ct))
            throw new DomainException("This phone number is already in use by another account of the same type.");

        user.ChangePhone(newPhone);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private bool IsTestOtp(string code) =>
        testOtpOptions.Value.Enabled
        && !hostEnvironment.IsProduction()
        && string.Equals(code.Trim(), testOtpOptions.Value.Code.Trim(), StringComparison.Ordinal);
}

// ─── Doctor Auth Service ──────────────────────────────────────────────────────

public class DoctorAuthService(
    IDoctorRepository doctorRepository,
    IOtpVerificationRepository otpVerificationRepository,
    IOtpService otpService,
    ISmsService smsService,
    IAuthService authService,
    IUnitOfWork unitOfWork,
    IOptions<TestOtpOptions> testOtpOptions,
    IHostEnvironment hostEnvironment) : IDoctorAuthService
{
    public async Task RequestOtpAsync(string badgeNumber, CancellationToken ct)
    {
        var doctor = await doctorRepository.GetByBadgeNumberAsync(badgeNumber, ct)
            ?? throw new DomainException("Doctor badge number not found.");

        if (testOtpOptions.Value.Enabled && !hostEnvironment.IsProduction())
            return;

        var otp = otpService.GenerateOtp();
        await otpVerificationRepository.AddAsync(new OtpVerification(doctor.PhoneNumber, otp, OtpPurposes.DoctorLogin), ct);
        await unitOfWork.SaveChangesAsync(ct);
        await smsService.SendOtpAsync(doctor.PhoneNumber, otp, ct);
    }

    public async Task<AuthResult> VerifyOtpAsync(string badgeNumber, string otpCode, CancellationToken ct)
    {
        var doctor = await doctorRepository.GetByBadgeNumberAsync(badgeNumber, ct)
            ?? throw new DomainException("Doctor badge number not found.");

        var useTestOtp = IsTestOtp(otpCode);
        if (!useTestOtp)
        {
            var latestOtp = await otpVerificationRepository.GetLatestActiveAsync(doctor.PhoneNumber, OtpPurposes.DoctorLogin, ct)
                ?? throw new DomainException("OTP not found or expired.");
            if (!latestOtp.Matches(otpCode))
                throw new DomainException("Invalid or expired OTP.");
            latestOtp.MarkUsed();
        }

        doctor.RecordLogin();
        var tokens = await authService.GenerateAuthTokensAsync(doctor, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new AuthResult(tokens.AccessToken, tokens.RefreshToken, doctor.Id, doctor.UserType.ToString(), doctor.FullName, true);
    }

    private bool IsTestOtp(string code) =>
        testOtpOptions.Value.Enabled
        && !hostEnvironment.IsProduction()
        && string.Equals(code.Trim(), testOtpOptions.Value.Code.Trim(), StringComparison.Ordinal);
}

// ─── Admin Auth Service ───────────────────────────────────────────────────────

public class AdminAuthService(
    IUserRepository userRepository,
    IDoctorRepository doctorRepository,
    IHealthcareCenterRepository centerRepository,
    IDoctorInvitationRepository invitationRepository,
    IRepository<DoctorHealthcareCenter> affiliationRepository,
    IPasswordService passwordService,
    IAuthService authService,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ISmsService smsService,
    IEmailService emailService,
    MediMind.Application.Features.Admin.IAuditLogger auditLogger) : IAdminAuthService
{
    // In-memory login failure tracking (consistent with in-memory refresh token store pattern)
    private static readonly Dictionary<string, (int Count, DateTime LockedUntil)> LoginFailures = [];
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    private const int MaxFailedAttempts = 5;

    public async Task<Guid> RegisterAsync(AdminRegisterRequest request, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(request.PhoneNumber);

        if (await userRepository.ExistsByEmailAsync(request.Email, ct))
            throw new DomainException("Email already registered.");
        if (await userRepository.ExistsByPhoneForRoleAsync(phone, UserType.Admin, ct))
            throw new DomainException("Phone number already registered for an admin account.");

        var admin = new HealthcareCenterAdmin(
            request.Email, phone, request.FullName,
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Gender.Other, null, "Owner");
        admin.SetPasswordHash(passwordService.HashPassword(request.Password));
        admin.Activate();

        await userRepository.AddAsync(admin, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return admin.Id;
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct)
    {
        var key = email.ToLowerInvariant();
        if (LoginFailures.TryGetValue(key, out var failure)
            && failure.Count >= MaxFailedAttempts
            && failure.LockedUntil > DateTime.UtcNow)
            throw new DomainException("Too many failed attempts. Account temporarily locked for 15 minutes.");

        var user = await userRepository.GetByEmailAsync(email, ct);
        if (user is null || user.UserType != UserType.Admin || !passwordService.VerifyPassword(password, user.PasswordHash))
        {
            var count = LoginFailures.TryGetValue(key, out var f) ? f.Count + 1 : 1;
            LoginFailures[key] = (count, DateTime.UtcNow.Add(LockDuration));
            await auditLogger.LogAsync(AuditActions.LoginFailure, user?.Id, "Admin", (user as HealthcareCenterAdmin)?.CenterId, "User", user?.Id, $"{{\"email\":\"{email}\"}}", ct);
            throw new DomainException("Invalid credentials.");
        }

        LoginFailures.Remove(key);
        var centerAdmin = user as HealthcareCenterAdmin;
        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(ct);

        await auditLogger.LogAsync(AuditActions.LoginSuccess, user.Id, "Admin", centerAdmin?.CenterId, "User", user.Id, null, ct);

        var tokens = await authService.GenerateAuthTokensAsync(user, ct);
        return new AuthResult(tokens.AccessToken, tokens.RefreshToken, user.Id, user.UserType.ToString(), user.FullName, true);
    }

    public async Task<DoctorCreatedResult> CreateDoctorAsync(CreateDoctorRequest request, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(request.PhoneNumber);

        var admin = await userRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), currentUser.UserId);
        if (admin.UserType != UserType.Admin)
            throw new ForbiddenException();

        if (await userRepository.ExistsByEmailAsync(request.Email, ct))
            throw new DomainException("An account with this email already exists.");
        if (await userRepository.ExistsByPhoneForRoleAsync(phone, UserType.Doctor, ct))
            throw new DomainException("Phone number already registered for a doctor account.");

        var centerAdmin = admin as HealthcareCenterAdmin;
        var centerName = "MediMind";
        HealthcareCenter? center = null;
        if (centerAdmin?.CenterId is { } centerId)
        {
            center = await centerRepository.GetByIdAsync(centerId, ct);
            if (center is not null) centerName = center.CenterName;
        }

        var badgeNumber = await GenerateUniqueBadgeNumberAsync(doctorRepository, ct);
        var doctor = new Doctor(
            request.Email,
            phone,
            request.FullName,
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Gender.Other,
            request.Specialization,
            badgeNumber,
            request.YearsOfExperience);
        doctor.SetLicenseNumber(request.LicenseNumber);
        doctor.SetPasswordHash(passwordService.HashPassword(Guid.NewGuid().ToString("N")));
        doctor.Activate();

        await doctorRepository.AddAsync(doctor, ct);
        await unitOfWork.SaveChangesAsync(ct);

        if (center is not null)
        {
            var affiliation = new DoctorHealthcareCenter(doctor.Id, center.Id, request.ConsultationFee);
            await affiliationRepository.AddAsync(affiliation, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        var startDate = DateTime.UtcNow.ToString("MMMM d, yyyy");
        try
        {
            await smsService.SendAsync(
                phone,
                $"Welcome to {centerName}, Dr. {request.FullName}! Your MediMind account is ready. " +
                $"Badge number: {badgeNumber}. Use it with OTP to log in. Start date: {startDate}.",
                ct);
        }
        catch (Exception) { }

        try
        {
            await emailService.SendAsync(
                request.Email,
                "Your MediMind Doctor Account is Ready",
                $"<p>Dear Dr. {request.FullName},</p>" +
                $"<p>Your doctor account at <strong>{centerName}</strong> has been created on MediMind.</p>" +
                $"<p><strong>Badge number:</strong> {badgeNumber}</p>" +
                $"<p><strong>Start date:</strong> {startDate}</p>" +
                $"<p>Use your badge number and OTP to log in to the MediMind Doctor app.</p>" +
                $"<p>Welcome to the team!</p>",
                ct);
        }
        catch (Exception) { }

        return new DoctorCreatedResult(doctor.Id, badgeNumber, "Doctor account created successfully. Badge number sent via SMS and email.");
    }

    public async Task<DoctorInvitationResult> InviteDoctorAsync(Guid centerId, InviteDoctorRequest request, CancellationToken ct)
    {
        if (currentUser.TenantId != centerId)
            throw new Domain.Exceptions.ForbiddenException();

        var invitePhone = !string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? PhoneNormalizer.Normalize(request.PhoneNumber)
            : string.Empty;

        if (await userRepository.ExistsByEmailAsync(request.Email, ct))
            throw new DomainException("An account with this email already exists.");

        var existing = await invitationRepository.GetByEmailAndCenterAsync(request.Email, centerId, ct);
        if (existing is not null && !existing.IsAccepted && existing.ExpiresAt > DateTime.UtcNow)
            throw new DomainException("An active invitation has already been sent to this email.");

        var center = await centerRepository.GetByIdAsync(centerId, ct)
            ?? throw new Domain.Exceptions.NotFoundException(nameof(HealthcareCenter), centerId);

        var invitation = new DoctorInvitation(
            centerId,
            request.Email,
            request.FullName,
            request.LicenseNumber,
            request.Specialization,
            request.YearsOfExperience,
            request.ConsultationFee,
            invitePhone);

        await invitationRepository.CreateAsync(invitation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var invitationLink = $"https://app.medimind.et/doctors/accept-invitation?token={invitation.Token}";
        try
        {
            await emailService.SendDoctorInvitationAsync(request.Email, request.FullName, center.CenterName, invitationLink, ct);
        }
        catch (Exception)
        {
            // Email failure must not roll back a successfully stored invitation
        }

        if (!string.IsNullOrWhiteSpace(invitePhone))
        {
            try
            {
                var smsMessage = $"You have been invited to join {center.CenterName} on MediMind as a doctor. Accept your invitation here: {invitationLink} (valid 48 hours)";
                await smsService.SendAsync(invitePhone, smsMessage, ct);
            }
            catch (Exception)
            {
                // SMS failure must not roll back a successfully stored invitation
            }
        }

        return new DoctorInvitationResult(invitation.Id, "Invitation sent to doctor's email and SMS. Valid for 48 hours.");
    }

    public async Task<AcceptInvitationResult> AcceptDoctorInvitationAsync(AcceptInvitationRequest request, CancellationToken ct)
    {
        var invitation = await invitationRepository.GetByTokenAsync(request.Token, ct)
            ?? throw new Domain.Exceptions.NotFoundException("Invitation", request.Token);

        if (invitation.ExpiresAt < DateTime.UtcNow)
            throw new DomainException("Invitation link expired. Resend invitation");

        if (invitation.IsAccepted)
            throw new DomainException("This invitation has already been used.");

        if (await userRepository.ExistsByEmailAsync(invitation.Email, ct))
            throw new DomainException("An account with this email already exists.");

        var badgeNumber = await GenerateUniqueBadgeNumberAsync(doctorRepository, ct);
        var doctor = new Doctor(
            invitation.Email,
            invitation.PhoneNumber,
            invitation.FullName,
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Gender.Other,
            invitation.Specialization,
            badgeNumber,
            invitation.YearsOfExperience);
        doctor.SetLicenseNumber(invitation.LicenseNumber);
        doctor.SetPasswordHash(passwordService.HashPassword(request.Password));
        doctor.Activate();

        await doctorRepository.AddAsync(doctor, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var affiliation = new DoctorHealthcareCenter(doctor.Id, invitation.CenterId, invitation.ConsultationFee);
        await affiliationRepository.AddAsync(affiliation, ct);

        invitation.Accept();
        await invitationRepository.UpdateAsync(invitation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new AcceptInvitationResult(doctor.Id, badgeNumber, "Account created successfully. Use your badge number for OTP login.");
    }

    public async Task<IReadOnlyList<PendingInvitationDto>> GetPendingInvitationsAsync(Guid centerId, CancellationToken ct)
    {
        if (currentUser.TenantId != centerId)
            throw new Domain.Exceptions.ForbiddenException();

        var invitations = await invitationRepository.GetPendingByCenterAsync(centerId, ct);
        return invitations.Select(i => new PendingInvitationDto(
            i.Id, i.FullName, i.Email, i.PhoneNumber, i.Specialization, i.LicenseNumber, i.ExpiresAt))
            .ToList();
    }

    private static async Task<string> GenerateUniqueBadgeNumberAsync(IDoctorRepository repo, CancellationToken ct)
    {
        for (var i = 0; i < 100; i++)
        {
            var candidate = Random.Shared.Next(0, 1_000_000).ToString("D6");
            if (await repo.GetByBadgeNumberAsync(candidate, ct) is null)
                return candidate;
        }
        throw new DomainException("Unable to generate unique badge number. Please try again.");
    }
}

// ─── SuperAdmin Auth Service ──────────────────────────────────────────────────

public class SuperAdminAuthService(
    IUserRepository userRepository,
    IPasswordService passwordService,
    IAuthService authService,
    IUnitOfWork unitOfWork) : ISuperAdminAuthService
{
    private static readonly Dictionary<string, (int Count, DateTime LockedUntil)> LoginFailures = [];
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    private const int MaxFailedAttempts = 5;

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct)
    {
        var key = email.ToLowerInvariant();
        if (LoginFailures.TryGetValue(key, out var failure)
            && failure.Count >= MaxFailedAttempts
            && failure.LockedUntil > DateTime.UtcNow)
            throw new DomainException("Too many failed attempts. Account temporarily locked for 15 minutes.");

        var user = await userRepository.GetByEmailAsync(email, ct);
        if (user is null || user.UserType != UserType.SuperAdmin || !passwordService.VerifyPassword(password, user.PasswordHash))
        {
            var count = LoginFailures.TryGetValue(key, out var f) ? f.Count + 1 : 1;
            LoginFailures[key] = (count, DateTime.UtcNow.Add(LockDuration));
            throw new DomainException("Invalid credentials.");
        }

        LoginFailures.Remove(key);
        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(ct);

        var tokens = await authService.GenerateAuthTokensAsync(user, ct);
        return new AuthResult(tokens.AccessToken, tokens.RefreshToken, user.Id, user.UserType.ToString(), user.FullName, true);
    }
}
