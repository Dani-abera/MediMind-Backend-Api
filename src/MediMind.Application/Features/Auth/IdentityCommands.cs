using FluentValidation;
using MediatR;
using MediMind.Application.Auth;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MediMind.Application.Features.Auth;

public record TokenResponse(string AccessToken, string RefreshToken, Guid UserId, string Role);

public static class OtpPurposes
{
    public const string DoctorLogin = "doctor_login";
    public const string PatientLogin = "patient_login";
}

public record AdminRegisterCommand(string FullName, string Email, string PhoneNumber, string Password) : IRequest<Guid>;
public record AdminLoginCommand(string Email, string Password) : IRequest<TokenResponse>;
public record AdminCreateDoctorCommand(string FullName, string PhoneNumber, string Specialization, int YearsOfExperience) : IRequest<DoctorCreatedResponse>;
public record DoctorLoginCommand(string BadgeNumber) : IRequest<Unit>;
public record DoctorVerifyOtpCommand(string BadgeNumber, string OtpCode) : IRequest<TokenResponse>;
public record PatientRequestOtpCommand(string PhoneNumber) : IRequest<Unit>;
public record PatientVerifyOtpCommand(string PhoneNumber, string OtpCode) : IRequest<TokenResponse>;
public record UpsertPatientProfileCommand(string FullName, DateOnly DateOfBirth, Gender Gender, string Address, string? MedicalHistory, string? Allergies, BloodType? BloodType) : IRequest<Unit>;
public record PatchPatientProfileCommand(string? FullName, DateOnly? DateOfBirth, Gender? Gender, string? Address, string? MedicalHistory, string? Allergies, BloodType? BloodType) : IRequest<Unit>;
public record DeletePatientProfileCommand : IRequest<Unit>;
public record GetPatientProfileQuery : IRequest<PatientProfileDto>;

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

public record DoctorCreatedResponse(Guid DoctorId, string BadgeNumber, string Message);

public class AdminRegisterValidator : AbstractValidator<AdminRegisterCommand>
{
    public AdminRegisterValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^\+251[0-9]{9}$");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public class AdminLoginValidator : AbstractValidator<AdminLoginCommand>
{
    public AdminLoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class AdminCreateDoctorValidator : AbstractValidator<AdminCreateDoctorCommand>
{
    public AdminCreateDoctorValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^\+251[0-9]{9}$");
        RuleFor(x => x.Specialization).NotEmpty().MaximumLength(100);
        RuleFor(x => x.YearsOfExperience).GreaterThanOrEqualTo(0);
    }
}

public class OtpRequestValidator : AbstractValidator<PatientRequestOtpCommand>
{
    public OtpRequestValidator()
    {
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^\+251[0-9]{9}$");
    }
}

public class DoctorLoginValidator : AbstractValidator<DoctorLoginCommand>
{
    public DoctorLoginValidator()
    {
        RuleFor(x => x.BadgeNumber).NotEmpty().Matches(@"^[0-9]{6}$");
    }
}

public class DoctorVerifyOtpValidator : AbstractValidator<DoctorVerifyOtpCommand>
{
    public DoctorVerifyOtpValidator()
    {
        RuleFor(x => x.BadgeNumber).NotEmpty().Matches(@"^[0-9]{6}$");
        RuleFor(x => x.OtpCode).NotEmpty().Length(6);
    }
}

public class PatientVerifyOtpValidator : AbstractValidator<PatientVerifyOtpCommand>
{
    public PatientVerifyOtpValidator()
    {
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^\+251[0-9]{9}$");
        RuleFor(x => x.OtpCode).NotEmpty().Length(6);
    }
}

public class UpsertPatientProfileValidator : AbstractValidator<UpsertPatientProfileCommand>
{
    public UpsertPatientProfileValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);
        RuleFor(x => x.DateOfBirth).LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow));
    }
}

public class AdminRegisterHandler(
    IUserRepository userRepository,
    IPasswordService passwordService,
    IUnitOfWork unitOfWork) : IRequestHandler<AdminRegisterCommand, Guid>
{
    public async Task<Guid> Handle(AdminRegisterCommand request, CancellationToken ct)
    {
        if (await userRepository.ExistsByEmailAsync(request.Email, ct))
            throw new DomainException("Email already registered.");
        if (await userRepository.ExistsByPhoneAsync(request.PhoneNumber, ct))
            throw new DomainException("Phone number already registered.");

        var admin = new HealthcareCenterAdmin(
            request.Email,
            request.PhoneNumber,
            request.FullName,
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Gender.Other,
            null,
            "Owner");
        admin.SetPasswordHash(passwordService.HashPassword(request.Password));
        admin.Activate();

        await userRepository.AddAsync(admin, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return admin.Id;
    }
}

public class AdminLoginHandler(
    IUserRepository userRepository,
    IPasswordService passwordService,
    IAuthService authService,
    IUnitOfWork unitOfWork) : IRequestHandler<AdminLoginCommand, TokenResponse>
{
    public async Task<TokenResponse> Handle(AdminLoginCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, ct)
            ?? throw new DomainException("Invalid credentials.");
        if (user.UserType != UserType.Admin)
            throw new ForbiddenException();
        if (!passwordService.VerifyPassword(request.Password, user.PasswordHash))
            throw new DomainException("Invalid credentials.");

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(ct);

        var tokens = await authService.GenerateAuthTokensAsync(user, ct);
        return new TokenResponse(tokens.AccessToken, tokens.RefreshToken, user.Id, user.UserType.ToString());
    }
}

public class AdminCreateDoctorHandler(
    IDoctorRepository doctorRepository,
    IUserRepository userRepository,
    IPasswordService passwordService,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : IRequestHandler<AdminCreateDoctorCommand, DoctorCreatedResponse>
{
    public async Task<DoctorCreatedResponse> Handle(AdminCreateDoctorCommand request, CancellationToken ct)
    {
        var admin = await userRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), currentUser.UserId);
        if (admin.UserType != UserType.Admin)
            throw new ForbiddenException();
        if (await userRepository.ExistsByPhoneAsync(request.PhoneNumber, ct))
            throw new DomainException("Phone number already registered.");

        var badgeNumber = await GenerateUniqueBadgeNumberAsync(doctorRepository, ct);
        var doctor = new Doctor(
            $"{badgeNumber}@doctor.medimind.local",
            request.PhoneNumber,
            request.FullName,
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Gender.Other,
            request.Specialization,
            badgeNumber,
            request.YearsOfExperience);
        doctor.SetPasswordHash(passwordService.HashPassword(Guid.NewGuid().ToString("N")));
        doctor.Activate();

        await doctorRepository.AddAsync(doctor, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new DoctorCreatedResponse(
            doctor.Id,
            badgeNumber,
            "Doctor created successfully. Use badge number for OTP login.");
    }

    private static async Task<string> GenerateUniqueBadgeNumberAsync(IDoctorRepository doctorRepository, CancellationToken ct)
    {
        for (var i = 0; i < 100; i++)
        {
            var candidate = Random.Shared.Next(0, 1_000_000).ToString("D6");
            if (await doctorRepository.GetByBadgeNumberAsync(candidate, ct) is null)
                return candidate;
        }

        throw new DomainException("Unable to generate unique badge number. Please try again.");
    }
}

public class DoctorLoginHandler(
    IDoctorRepository doctorRepository,
    IOtpService otpService,
    IOtpVerificationRepository otpVerificationRepository,
    ISmsService smsService,
    IUnitOfWork unitOfWork) : IRequestHandler<DoctorLoginCommand, Unit>
{
    public async Task<Unit> Handle(DoctorLoginCommand request, CancellationToken ct)
    {
        var doctor = await doctorRepository.GetByBadgeNumberAsync(request.BadgeNumber, ct)
            ?? throw new DomainException("Doctor badge number not found.");
        var otp = otpService.GenerateOtp();
        await otpVerificationRepository.AddAsync(new OtpVerification(doctor.PhoneNumber, otp, OtpPurposes.DoctorLogin), ct);
        await unitOfWork.SaveChangesAsync(ct);
        await smsService.SendOtpAsync(doctor.PhoneNumber, otp, ct);
        return Unit.Value;
    }
}

public class DoctorVerifyOtpHandler(
    IDoctorRepository doctorRepository,
    IOtpVerificationRepository otpVerificationRepository,
    IAuthService authService,
    IUnitOfWork unitOfWork,
    IOptions<TestOtpOptions> testOtpOptions,
    IHostEnvironment hostEnvironment) : IRequestHandler<DoctorVerifyOtpCommand, TokenResponse>
{
    public async Task<TokenResponse> Handle(DoctorVerifyOtpCommand request, CancellationToken ct)
    {
        var doctor = await doctorRepository.GetByBadgeNumberAsync(request.BadgeNumber, ct)
            ?? throw new DomainException("Doctor badge number not found.");

        var useDefaultOtp = IsDefaultOtpAccepted(request.OtpCode, testOtpOptions.Value, hostEnvironment);
        if (!useDefaultOtp)
        {
            var latestOtp = await otpVerificationRepository.GetLatestActiveAsync(doctor.PhoneNumber, OtpPurposes.DoctorLogin, ct)
                ?? throw new DomainException("OTP not found.");
            if (!latestOtp.Matches(request.OtpCode))
                throw new DomainException("Invalid or expired OTP.");
            latestOtp.MarkUsed();
        }

        doctor.RecordLogin();
        var tokens = await authService.GenerateAuthTokensAsync(doctor, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new TokenResponse(tokens.AccessToken, tokens.RefreshToken, doctor.Id, doctor.UserType.ToString());
    }

    private static bool IsDefaultOtpAccepted(string inputOtp, TestOtpOptions options, IHostEnvironment hostEnvironment)
    {
        return options.Enabled
               && !hostEnvironment.IsProduction()
               && string.Equals(inputOtp.Trim(), options.Code.Trim(), StringComparison.Ordinal);
    }
}

public class PatientRequestOtpHandler(
    IOtpService otpService,
    IOtpVerificationRepository otpVerificationRepository,
    ISmsService smsService,
    IUnitOfWork unitOfWork) : IRequestHandler<PatientRequestOtpCommand, Unit>
{
    public async Task<Unit> Handle(PatientRequestOtpCommand request, CancellationToken ct)
    {
        var otp = otpService.GenerateOtp();
        await otpVerificationRepository.AddAsync(new OtpVerification(request.PhoneNumber, otp, OtpPurposes.PatientLogin), ct);
        await unitOfWork.SaveChangesAsync(ct);
        await smsService.SendOtpAsync(request.PhoneNumber, otp, ct);
        return Unit.Value;
    }
}

public class PatientVerifyOtpHandler(
    IUserRepository userRepository,
    IPatientRepository patientRepository,
    IOtpVerificationRepository otpVerificationRepository,
    IPasswordService passwordService,
    IAuthService authService,
    IUnitOfWork unitOfWork,
    IOptions<TestOtpOptions> testOtpOptions,
    IHostEnvironment hostEnvironment) : IRequestHandler<PatientVerifyOtpCommand, TokenResponse>
{
    public async Task<TokenResponse> Handle(PatientVerifyOtpCommand request, CancellationToken ct)
    {
        var useDefaultOtp = IsDefaultOtpAccepted(request.OtpCode, testOtpOptions.Value, hostEnvironment);
        OtpVerification? latestOtp = null;
        if (!useDefaultOtp)
        {
            latestOtp = await otpVerificationRepository.GetLatestActiveAsync(request.PhoneNumber, OtpPurposes.PatientLogin, ct)
                ?? throw new DomainException("OTP not found.");
            if (!latestOtp.Matches(request.OtpCode))
                throw new DomainException("Invalid or expired OTP.");
        }

        var existing = await userRepository.GetByPhoneAsync(request.PhoneNumber, ct);
        if (existing is not null && existing.UserType != UserType.Patient)
            throw new DomainException("This phone number is in use by another role.");

        Patient patient;
        if (existing is Patient p)
        {
            patient = p;
        }
        else
        {
            patient = new Patient(
                $"{request.PhoneNumber.Replace("+", string.Empty)}@patient.medimind.local",
                request.PhoneNumber,
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

        var tokens = await authService.GenerateAuthTokensAsync(patient, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new TokenResponse(tokens.AccessToken, tokens.RefreshToken, patient.Id, patient.UserType.ToString());
    }

    private static bool IsDefaultOtpAccepted(string inputOtp, TestOtpOptions options, IHostEnvironment hostEnvironment)
    {
        return options.Enabled
               && !hostEnvironment.IsProduction()
               && string.Equals(inputOtp.Trim(), options.Code.Trim(), StringComparison.Ordinal);
    }
}

public class UpsertPatientProfileHandler(
    IPatientRepository patientRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : IRequestHandler<UpsertPatientProfileCommand, Unit>
{
    public async Task<Unit> Handle(UpsertPatientProfileCommand request, CancellationToken ct)
    {
        var patient = await patientRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(Patient), currentUser.UserId);
        patient.UpdateIdentityProfile(request.FullName, request.DateOfBirth, request.Gender);
        patient.UpdateMedicalProfile(request.Address, request.BloodType, request.Allergies, null, null, request.MedicalHistory, null, null);
        await unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class PatchPatientProfileHandler(
    IPatientRepository patientRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : IRequestHandler<PatchPatientProfileCommand, Unit>
{
    public async Task<Unit> Handle(PatchPatientProfileCommand request, CancellationToken ct)
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
        return Unit.Value;
    }
}

public class DeletePatientProfileHandler(
    IPatientRepository patientRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : IRequestHandler<DeletePatientProfileCommand, Unit>
{
    public async Task<Unit> Handle(DeletePatientProfileCommand request, CancellationToken ct)
    {
        var patient = await patientRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(Patient), currentUser.UserId);
        await patientRepository.DeleteAsync(patient, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class GetPatientProfileHandler(
    IPatientRepository patientRepository,
    ICurrentUser currentUser) : IRequestHandler<GetPatientProfileQuery, PatientProfileDto>
{
    public async Task<PatientProfileDto> Handle(GetPatientProfileQuery request, CancellationToken ct)
    {
        var patient = await patientRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(Patient), currentUser.UserId);
        return new PatientProfileDto(
            patient.Id,
            patient.PhoneNumber,
            patient.FullName,
            patient.DateOfBirth,
            patient.Gender,
            patient.Address,
            patient.MedicalHistory,
            patient.Allergies,
            patient.BloodType);
    }
}
