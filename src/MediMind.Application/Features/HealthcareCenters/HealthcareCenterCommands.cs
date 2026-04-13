using FluentValidation;
using MediatR;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.HealthcareCenters;

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record HealthcareCenterDto(
    Guid Id,
    string CenterName,
    string CenterType,
    string Address,
    string City,
    string Region,
    string PhoneNumber,
    string Email,
    string? ProfileImageUrl,
    Dictionary<string, string> WorkingHours,
    List<string> ServicesOffered,
    List<string> Specializations,
    string SubscriptionStatus,
    int SlotDurationMinutes,
    int AdvanceBookingDays,
    decimal? Latitude,
    decimal? Longitude,
    int TotalDoctors);

public record DoctorSummaryDto(
    Guid Id,
    string FullName,
    string Specialization,
    int YearsOfExperience,
    decimal ConsultationFee,
    bool IsAvailableToday,
    string? ProfileImageUrl);

// ═══════════════════════════════════════════════════════════════════════════════
// REGISTER HEALTHCARE CENTER (Super Admin or self-registration)
// ═══════════════════════════════════════════════════════════════════════════════

public record RegisterHealthcareCenterCommand(
    string CenterName,
    string CenterType,
    string LicenseNumber,
    string Address,
    string City,
    string Region,
    string PhoneNumber,
    string Email,
    Dictionary<string, string> WorkingHours,
    List<string> ServicesOffered,
    // First admin account details
    string AdminEmail,
    string AdminPhoneNumber,
    string AdminFullName,
    DateOnly AdminDateOfBirth,
    string AdminPassword
) : IRequest<RegisterHealthcareCenterResult>;

public record RegisterHealthcareCenterResult(
    Guid CenterId,
    Guid AdminId,
    string Message);

public class RegisterHealthcareCenterValidator : AbstractValidator<RegisterHealthcareCenterCommand>
{
    public RegisterHealthcareCenterValidator()
    {
        RuleFor(x => x.CenterName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CenterType)
            .NotEmpty()
            .Must(t => new[] { "Hospital", "Clinic", "Health Center", "Specialized Center" }.Contains(t))
            .WithMessage("Center type must be: Hospital, Clinic, Health Center, or Specialized Center.");
        RuleFor(x => x.LicenseNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).EmailAddress();
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+251[0-9]{9}$")
            .WithMessage("Phone must be in Ethiopian format: +251XXXXXXXXX");
        RuleFor(x => x.WorkingHours).NotEmpty()
            .WithMessage("At least one working day must be specified.");
        RuleFor(x => x.ServicesOffered).NotEmpty()
            .WithMessage("At least one service must be specified.");
        RuleFor(x => x.AdminEmail).EmailAddress();
        RuleFor(x => x.AdminPassword).MinimumLength(8);
    }
}

public class RegisterHealthcareCenterHandler(
    IHealthcareCenterRepository centerRepository,
    IUserRepository userRepository,
    IPasswordService passwordService,
    IOtpService otpService,
    ISmsService smsService,
    IEmailService emailService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterHealthcareCenterCommand, RegisterHealthcareCenterResult>
{
    public async Task<RegisterHealthcareCenterResult> Handle(
        RegisterHealthcareCenterCommand request, CancellationToken ct)
    {
        if (await centerRepository.ExistsByLicenseAsync(request.LicenseNumber, ct))
            throw new DomainException("A healthcare center with this license number already exists.");

        if (await userRepository.ExistsByEmailAsync(request.AdminEmail, ct))
            throw new DomainException("Email already in use. Use a different email or login to existing account.");

        // Create center first
        var center = new HealthcareCenter(
            request.CenterName,
            request.CenterType,
            request.LicenseNumber,
            request.Address,
            request.City,
            request.Region,
            request.PhoneNumber,
            request.Email,
            request.WorkingHours,
            request.ServicesOffered);

        await centerRepository.AddAsync(center, ct);

        // Create first admin account for this center
        var admin = new HealthcareCenterAdmin(
            request.AdminEmail,
            request.AdminPhoneNumber,
            request.AdminFullName,
            request.AdminDateOfBirth,
            Gender.Other,  // Can be updated later
            center.Id,
            "Manager");

        admin.SetPasswordHash(passwordService.HashPassword(request.AdminPassword));
        var otp = otpService.GenerateOtp();
        admin.GenerateOtp(otp);
        admin.Activate(); // Auto-activate admin on center registration

        await unitOfWork.SaveChangesAsync(ct);

        // Send welcome notifications
        await smsService.SendAsync(
            request.AdminPhoneNumber,
            $"Welcome to MediMind! Your healthcare center '{request.CenterName}' has been registered. Your OTP: {otp}",
            ct);

        return new RegisterHealthcareCenterResult(
            center.Id, admin.Id,
            $"Healthcare center '{request.CenterName}' registered successfully. Check your phone for verification code.");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// REGISTER DOCTOR (Admin adds doctor to center) FR-020
// ═══════════════════════════════════════════════════════════════════════════════

public record RegisterDoctorCommand(
    Guid CenterId,
    Guid AdminId,
    string FullName,
    string LicenseNumber,
    string Specialization,
    string Email,
    string PhoneNumber,
    int YearsOfExperience,
    decimal ConsultationFee,
    string? Qualifications,
    List<string>? LanguagesSpoken
) : IRequest<RegisterDoctorResult>;

public record RegisterDoctorResult(Guid DoctorId, string Message);

public class RegisterDoctorValidator : AbstractValidator<RegisterDoctorCommand>
{
    public RegisterDoctorValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LicenseNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Specialization).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress();
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+251[0-9]{9}$")
            .WithMessage("Phone must be in Ethiopian format: +251XXXXXXXXX");
        RuleFor(x => x.YearsOfExperience).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ConsultationFee)
            .GreaterThan(0).WithMessage("Price must be greater than 0 ETB.");
    }
}

public class RegisterDoctorHandler(
    IDoctorRepository doctorRepository,
    IUserRepository userRepository,
    IHealthcareCenterRepository centerRepository,
    IRepository<DoctorHealthcareCenter> doctorCenterRepository,
    IPasswordService passwordService,
    IEmailService emailService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterDoctorCommand, RegisterDoctorResult>
{
    public async Task<RegisterDoctorResult> Handle(RegisterDoctorCommand request, CancellationToken ct)
    {
        var center = await centerRepository.GetByIdAsync(request.CenterId, ct)
                     ?? throw new NotFoundException(nameof(HealthcareCenter), request.CenterId);

        // Tenant isolation: admin can only add doctors to their center
        if (!center.Admins.Any(a => a.Id == request.AdminId))
            throw new TenantIsolationException();

        // Check if doctor already exists (might work at multiple centers)
        Doctor doctor;
        var existingDoctor = await doctorRepository.GetByLicenseAsync(request.LicenseNumber, ct);

        if (existingDoctor is not null)
        {
            // Doctor exists — just add the new center affiliation
            doctor = existingDoctor;
        }
        else
        {
            // New doctor — check email uniqueness
            if (await userRepository.ExistsByEmailAsync(request.Email, ct))
                throw new DomainException("Please enter a valid email address.");

            // Generate a temporary password (doctor will reset via OTP)
            var tempPassword = Convert.ToBase64String(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(12));

            doctor = new Doctor(
                request.Email,
                request.PhoneNumber,
                request.FullName,
                DateOnly.FromDateTime(DateTime.Today.AddYears(-30)), // Placeholder — doctor updates profile
                Gender.Other,
                request.Specialization,
                request.LicenseNumber,
                request.YearsOfExperience);

            doctor.SetPasswordHash(passwordService.HashPassword(tempPassword));
            doctor.UpdateProfessionalInfo(
                request.Specialization,
                request.YearsOfExperience,
                request.Qualifications,
                request.LanguagesSpoken);

            await doctorRepository.AddAsync(doctor, ct);
        }

        // Create doctor-center affiliation with consultation fee
        var affiliation = new DoctorHealthcareCenter(doctor.Id, request.CenterId, request.ConsultationFee);
        await doctorCenterRepository.AddAsync(affiliation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Send invitation email to doctor
        await emailService.SendDoctorInvitationAsync(
            request.Email,
            request.FullName,
            center.CenterName,
            $"https://portal.medimind.et/accept-invitation/{doctor.Id}",
            ct);

        return new RegisterDoctorResult(
            doctor.Id,
            $"Dr. {request.FullName} registered successfully. Invitation sent to {request.Email}.");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// CONFIGURE DOCTOR SCHEDULE (Admin) FR-021
// ═══════════════════════════════════════════════════════════════════════════════

public record ConfigureDoctorScheduleCommand(
    Guid DoctorId,
    Guid CenterId,
    Guid AdminId,
    List<string> WorkingDays,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotDuration,
    TimeOnly? BreakStart,
    TimeOnly? BreakEnd
) : IRequest<Unit>;

public class ConfigureDoctorScheduleHandler(
    IRepository<DoctorSchedule> scheduleRepository,
    IDoctorRepository doctorRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ConfigureDoctorScheduleCommand, Unit>
{
    public async Task<Unit> Handle(ConfigureDoctorScheduleCommand request, CancellationToken ct)
    {
        var doctor = await doctorRepository.GetByIdAsync(request.DoctorId, ct)
                     ?? throw new NotFoundException(nameof(Doctor), request.DoctorId);

        // Verify doctor works at this center
        if (!doctor.DoctorHealthcareCenters.Any(dhc => dhc.CenterId == request.CenterId && dhc.IsActive))
            throw new DomainException("Doctor is not affiliated with this healthcare center.");

        // Check if schedule already exists for this doctor-center
        var existingSchedule = doctor.Schedules.FirstOrDefault(s => s.CenterId == request.CenterId);

        if (existingSchedule is not null)
        {
            // Update existing schedule — in real implementation use reflection or recreate
            await scheduleRepository.DeleteAsync(existingSchedule, ct);
        }

        var schedule = new DoctorSchedule(
            request.DoctorId,
            request.CenterId,
            request.WorkingDays,
            request.StartTime,
            request.EndTime,
            request.SlotDuration,
            request.BreakStart,
            request.BreakEnd);

        await scheduleRepository.AddAsync(schedule, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Unit.Value;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SEARCH HEALTHCARE CENTERS (Patient — FR-008)
// ═══════════════════════════════════════════════════════════════════════════════

public record SearchHealthcareCentersQuery(
    string? City,
    string? Specialization,
    string? SearchText,
    decimal? PatientLatitude,
    decimal? PatientLongitude
) : IRequest<IReadOnlyList<HealthcareCenterDto>>;

public class SearchHealthcareCentersHandler(IHealthcareCenterRepository centerRepository)
    : IRequestHandler<SearchHealthcareCentersQuery, IReadOnlyList<HealthcareCenterDto>>
{
    public async Task<IReadOnlyList<HealthcareCenterDto>> Handle(
        SearchHealthcareCentersQuery request, CancellationToken ct)
    {
        var centers = await centerRepository.GetActiveSubscriptionsAsync(ct);

        var filtered = centers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.City))
            filtered = filtered.Where(c =>
                c.City.Contains(request.City, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.Specialization))
            filtered = filtered.Where(c =>
                c.Specializations.Any(s =>
                    s.Contains(request.Specialization, StringComparison.OrdinalIgnoreCase)));

        if (!string.IsNullOrWhiteSpace(request.SearchText))
            filtered = filtered.Where(c =>
                c.CenterName.Contains(request.SearchText, StringComparison.OrdinalIgnoreCase) ||
                c.Address.Contains(request.SearchText, StringComparison.OrdinalIgnoreCase));

        return filtered.Select(c => new HealthcareCenterDto(
            c.Id,
            c.CenterName,
            c.CenterType,
            c.Address,
            c.City,
            c.Region,
            c.PhoneNumber,
            c.Email,
            c.ProfileImageUrl,
            c.WorkingHours,
            c.ServicesOffered,
            c.Specializations,
            c.SubscriptionStatus.ToString(),
            c.SlotDurationMinutes,
            c.AdvanceBookingDays,
            c.Latitude,
            c.Longitude,
            c.DoctorHealthcareCenters.Count(dhc => dhc.IsActive)
        )).ToList();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// GET DOCTORS BY CENTER (Patient — FR-009)
// ═══════════════════════════════════════════════════════════════════════════════

public record GetDoctorsByCenterQuery(Guid CenterId, string? Specialization) : IRequest<IReadOnlyList<DoctorSummaryDto>>;

public class GetDoctorsByCenterHandler(IDoctorRepository doctorRepository)
    : IRequestHandler<GetDoctorsByCenterQuery, IReadOnlyList<DoctorSummaryDto>>
{
    public async Task<IReadOnlyList<DoctorSummaryDto>> Handle(
        GetDoctorsByCenterQuery request, CancellationToken ct)
    {
        var doctors = await doctorRepository.GetByCenterAsync(request.CenterId, ct);

        if (!string.IsNullOrWhiteSpace(request.Specialization))
            doctors = doctors
                .Where(d => d.Specialization.Contains(request.Specialization,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

        var today = DateOnly.FromDateTime(DateTime.Today);

        return doctors.Select(d =>
        {
            var affiliation = d.DoctorHealthcareCenters
                .FirstOrDefault(dhc => dhc.CenterId == request.CenterId);
            var schedule = d.Schedules.FirstOrDefault(s => s.CenterId == request.CenterId);
            var isAvailableToday = schedule?.IsWorkingDay(today) ?? false;

            return new DoctorSummaryDto(
                d.Id,
                d.FullName,
                d.Specialization,
                d.YearsOfExperience,
                affiliation?.ConsultationFee ?? 0,
                isAvailableToday,
                d.ProfileImageUrl);
        }).ToList();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// UPDATE TENANT CONFIGURATION (Admin — FR-030)
// ═══════════════════════════════════════════════════════════════════════════════

public record UpdateCenterConfigurationCommand(
    Guid CenterId,
    Guid AdminId,
    int SlotDurationMinutes,
    int AdvanceBookingDays,
    int CancellationHours,
    bool AutoApproveAppointments
) : IRequest<Unit>;

public class UpdateCenterConfigurationHandler(
    IHealthcareCenterRepository centerRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateCenterConfigurationCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCenterConfigurationCommand request, CancellationToken ct)
    {
        var center = await centerRepository.GetByIdAsync(request.CenterId, ct)
                     ?? throw new NotFoundException(nameof(HealthcareCenter), request.CenterId);

        if (!center.Admins.Any(a => a.Id == request.AdminId))
            throw new TenantIsolationException();

        center.UpdateConfiguration(
            request.SlotDurationMinutes,
            request.AdvanceBookingDays,
            request.CancellationHours,
            request.AutoApproveAppointments);

        await unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
