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

// ═══════════════════════════════════════════════════════════════════════════════
// REGISTER PATIENT
// ═══════════════════════════════════════════════════════════════════════════════

public record RegisterPatientCommand(
    string Email,
    string PhoneNumber,
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    string Password
) : IRequest<RegisterResult>;

public record RegisterResult(Guid UserId, string Message);

public class RegisterPatientValidator : AbstractValidator<RegisterPatientCommand>
{
    public RegisterPatientValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Please enter a valid email address.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+251[0-9]{9}$").WithMessage("Phone number must be in Ethiopian format: +251XXXXXXXXX");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100);

        RuleFor(x => x.DateOfBirth)
            .Must(dob => dob <= DateOnly.FromDateTime(DateTime.Today.AddYears(-18)))
            .WithMessage("Patient must be at least 18 years old.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.");
    }
}

public class RegisterPatientHandler(
    IUserRepository userRepository,
    IPatientRepository patientRepository,
    IPasswordService passwordService,
    IOtpService otpService,
    ISmsService smsService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterPatientCommand, RegisterResult>
{
    public async Task<RegisterResult> Handle(RegisterPatientCommand request, CancellationToken ct)
    {
        if (await userRepository.ExistsByEmailAsync(request.Email, ct))
            throw new DomainException("Account already exists. Please login.");

        if (await userRepository.ExistsByPhoneAsync(request.PhoneNumber, ct))
            throw new DomainException("Phone number already registered. Please login.");

        var patient = new Patient(
            request.Email,
            request.PhoneNumber,
            request.FullName,
            request.DateOfBirth,
            request.Gender);

        patient.SetPasswordHash(passwordService.HashPassword(request.Password));

        var otp = otpService.GenerateOtp();
        patient.GenerateOtp(otp, expiryMinutes: 5);

        await patientRepository.AddAsync(patient, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Send OTP via Geez SMS Gateway
        await smsService.SendOtpAsync(request.PhoneNumber, otp, ct);

        return new RegisterResult(patient.Id, "OTP sent to your phone number. Please verify.");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// VERIFY OTP & ACTIVATE
// ═══════════════════════════════════════════════════════════════════════════════

public record VerifyOtpCommand(string PhoneNumber, string OtpCode) : IRequest<AuthResult>;

public record AuthResult(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string UserType,
    string FullName);

public class VerifyOtpValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpValidator()
    {
        RuleFor(x => x.PhoneNumber).NotEmpty();
        RuleFor(x => x.OtpCode).NotEmpty().Length(6).WithMessage("OTP must be 6 digits.");
    }
}

public class VerifyOtpHandler(
    IUserRepository userRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    IOptions<TestOtpOptions> testOtpOptions,
    IHostEnvironment hostEnvironment)
    : IRequestHandler<VerifyOtpCommand, AuthResult>
{
    public async Task<AuthResult> Handle(VerifyOtpCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByPhoneAsync(request.PhoneNumber, ct)
                   ?? throw new NotFoundException(nameof(User), request.PhoneNumber);

        var testOtp = testOtpOptions.Value;
        var useTestOtp = testOtp.Enabled
                         && !hostEnvironment.IsProduction()
                         && string.Equals(
                             request.OtpCode.Trim(),
                             testOtp.Code.Trim(),
                             StringComparison.Ordinal);

        if (!useTestOtp && user.IsOtpLocked)
            throw new DomainException("Too many failed attempts. Account temporarily locked for 15 minutes.");

        if (useTestOtp)
            user.ResetOtpState();
        else if (!user.ValidateOtp(request.OtpCode))
            throw new DomainException($"Invalid verification code. Please try again.");

        if (user.Status == UserStatus.Pending)
            user.Activate();

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(ct);

        var (accessToken, refreshToken) = tokenService.GenerateTokens(user.Id, user.UserType.ToString(), null);

        return new AuthResult(accessToken, refreshToken, user.Id, user.UserType.ToString(), user.FullName);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// LOGIN (Phone + OTP)
// ═══════════════════════════════════════════════════════════════════════════════

public record SendLoginOtpCommand(string PhoneNumber) : IRequest<Unit>;

public class SendLoginOtpHandler(
    IUserRepository userRepository,
    IOtpService otpService,
    ISmsService smsService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SendLoginOtpCommand, Unit>
{
    public async Task<Unit> Handle(SendLoginOtpCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByPhoneAsync(request.PhoneNumber, ct)
                   ?? throw new DomainException("Phone number not found. Please register.");

        if (user.Status == UserStatus.Suspended)
            throw new DomainException("Account suspended. Contact support at support@medimind.et");

        var otp = otpService.GenerateOtp();
        user.GenerateOtp(otp, expiryMinutes: 5);

        await unitOfWork.SaveChangesAsync(ct);
        await smsService.SendOtpAsync(request.PhoneNumber, otp, ct);

        return Unit.Value;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DOCTOR LOGIN (Badge Number + OTP)
// ═══════════════════════════════════════════════════════════════════════════════

public record SendDoctorLoginOtpCommand(string LicenseNumber) : IRequest<Unit>;

public class SendDoctorLoginOtpHandler(
    IDoctorRepository doctorRepository,
    IOtpService otpService,
    ISmsService smsService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SendDoctorLoginOtpCommand, Unit>
{
    public async Task<Unit> Handle(SendDoctorLoginOtpCommand request, CancellationToken ct)
    {
        var doctor = await doctorRepository.GetByLicenseAsync(request.LicenseNumber, ct)
                     ?? throw new DomainException("Badge/License number not found. Please contact your healthcare center.");

        if (doctor.Status == UserStatus.Suspended)
            throw new DomainException("Account suspended. Contact support.");

        var otp = otpService.GenerateOtp();
        doctor.GenerateOtp(otp, expiryMinutes: 5);

        await unitOfWork.SaveChangesAsync(ct);
        await smsService.SendOtpAsync(doctor.PhoneNumber, otp, ct);

        return Unit.Value;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// REFRESH TOKEN
// ═══════════════════════════════════════════════════════════════════════════════

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResult>;

public class RefreshTokenHandler(
    ITokenService tokenService,
    IUserRepository userRepository)
    : IRequestHandler<RefreshTokenCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        if (!tokenService.ValidateRefreshToken(request.RefreshToken, out var userId))
            throw new DomainException("Session expired. Please login.");

        var user = await userRepository.GetByIdAsync(userId, ct)
                   ?? throw new NotFoundException(nameof(User), userId);

        var (accessToken, refreshToken) = tokenService.GenerateTokens(user.Id, user.UserType.ToString(), null);
        return new AuthResult(accessToken, refreshToken, user.Id, user.UserType.ToString(), user.FullName);
    }
}
