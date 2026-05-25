using MediMind.Application.Features.Admin;
using MediMind.Application.Features.Appointments;
using MediMind.Application.Features.Payments;
using MediMind.Application.Features.SuperAdmin;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MediMind.Application.Features.CenterManagement;

public interface IHealthcareCenterService
{
    Task<CenterResponseDto> RegisterCenterAsync(RegisterCenterDto dto, Guid adminUserId);
    Task<CenterResponseDto> GetByIdAsync(Guid centerId);
    Task<PagedResult<CenterResponseDto>> SearchAsync(CenterSearchDto search);
    Task<IEnumerable<CenterResponseDto>> SearchNearbyAsync(double latitude, double longitude, double radiusKm);
    Task<CenterResponseDto> UpdateConfigurationAsync(Guid centerId, CenterConfigurationDto dto, Guid adminId);
    Task<AdminCenterConfigDto> GetAdminConfigAsync(Guid centerId, Guid adminId);
    Task<AdminCenterConfigDto> UpdateAdminConfigAsync(Guid centerId, AdminCenterConfigUpdateDto dto, Guid adminId);
    Task<AdminBookingRulesDto> GetBookingRulesAsync(Guid centerId, Guid adminId);
    Task<AdminBookingRulesDto> UpdateBookingRulesAsync(Guid centerId, AdminBookingRulesDto dto, Guid adminId);
    Task<AdminWorkingHoursDto> GetWorkingHoursAsync(Guid centerId, Guid adminId);
    Task<AdminWorkingHoursDto> UpdateWorkingHoursAsync(Guid centerId, AdminWorkingHoursDto dto, Guid adminId);
    Task<DoctorCenterRelationDto> AddDoctorToCenterAsync(Guid centerId, AddDoctorDto dto, Guid adminId);
    Task<DoctorCenterRelationDto> UpdateConsultationFeeAsync(Guid centerId, Guid doctorId, UpdateConsultationFeeDto dto, Guid adminId, CancellationToken ct = default);
    Task<bool> RemoveDoctorFromCenterAsync(Guid centerId, Guid doctorId, Guid adminId);
    Task<IEnumerable<DoctorResponseDto>> GetDoctorsAsync(Guid centerId);
    Task<IEnumerable<AdminDoctorRosterItemDto>> GetAdminDoctorRosterAsync(Guid centerId, Guid adminId, CancellationToken ct = default);
    Task<SubscriptionPaymentInitiationDto> InitiateSubscriptionPaymentAsync(Guid centerId, Guid planId, SubscriptionBillingCycle billingCycle, Guid adminId, CancellationToken ct = default);
}

public interface IAnalyticsService
{
    Task<AnalyticsDashboardDto> GetDashboardAnalyticsAsync(Guid centerId, Guid adminId, DateOnly startDate, DateOnly endDate);
}

public class HealthcareCenterService(
    IHealthcareCenterRepository centerRepository,
    IDoctorRepository doctorRepository,
    IUserRepository userRepository,
    IAppointmentAvailabilityService appointmentAvailabilityService,
    ILogger<HealthcareCenterService> logger,
    IUnitOfWork unitOfWork,
    IAuditLogger auditLogger,
    ISubscriptionPlanService subscriptionPlanService,
    IPaymentRepository paymentRepository,
    IChapaClient chapaClient,
    IChapaConfiguration chapaConfiguration,
    IPaymentConfiguration paymentConfiguration) : IHealthcareCenterService
{
    public async Task<CenterResponseDto> RegisterCenterAsync(RegisterCenterDto dto, Guid adminUserId)
    {
        if (await centerRepository.GetByLicenseAsync(dto.LicenseNumber) is not null)
            throw new DomainException("A healthcare center with this license number already exists.");

        var workingHours = BuildWorkingHoursDictionary(dto.WorkingHours);
        var center = new HealthcareCenter(
            dto.CenterName,
            dto.CenterType,
            dto.LicenseNumber,
            dto.Address,
            dto.City,
            dto.Region,
            dto.PhoneNumber,
            dto.Email,
            workingHours,
            dto.ServicesOffered);

        if (dto.Specializations is { Count: > 0 })
        {
            // Preserve in config JSON list through existing aggregate field update pattern.
            center.UpdateConfiguration(center.SlotDurationMinutes, center.AdvanceBookingDays, center.CancellationHours, center.AutoApproveAppointments);
        }

        if (dto.Latitude.HasValue && dto.Longitude.HasValue)
            center.SetLocation((decimal)dto.Latitude.Value, (decimal)dto.Longitude.Value);

        await centerRepository.CreateAsync(center);
        var admin = await userRepository.GetByIdAsync(adminUserId);
        if (admin is HealthcareCenterAdmin centerAdmin)
            centerAdmin.AssignCenter(center.Id);
        await unitOfWork.SaveChangesAsync();

        logger.LogInformation("Center {CenterId} registered by admin {AdminUserId}", center.Id, adminUserId);

        return await MapCenter(center, null);
    }

    public async Task<CenterResponseDto> GetByIdAsync(Guid centerId)
    {
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        return await MapCenter(center, null);
    }

    public async Task<PagedResult<CenterResponseDto>> SearchAsync(CenterSearchDto search)
    {
        var result = await centerRepository.SearchAsync(search);
        var items = result.Items.AsEnumerable();

        if (search.AvailableOnDate.HasValue)
        {
            var date = search.AvailableOnDate.Value;
            var available = new List<Domain.Entities.HealthcareCenter>();
            foreach (var center in items)
            {
                var doctors = await centerRepository.GetDoctorsAsync(center.Id);
                var hasSlot = false;
                foreach (var dc in doctors.Where(d => d.IsActive))
                {
                    var slots = await appointmentAvailabilityService.GetAvailableSlotsAsync(dc.DoctorId, center.Id, date);
                    if (slots.Any(s => s.IsAvailable)) { hasSlot = true; break; }
                }
                if (hasSlot) available.Add(center);
            }
            items = available;
        }

        var mapped = new List<CenterResponseDto>();
        foreach (var center in items)
            mapped.Add(await MapCenter(center, null));

        return new PagedResult<CenterResponseDto>(mapped, result.Page, result.PageSize,
            search.AvailableOnDate.HasValue ? mapped.Count : result.TotalCount);
    }

    public async Task<IEnumerable<CenterResponseDto>> SearchNearbyAsync(double latitude, double longitude, double radiusKm)
    {
        var centers = (await centerRepository.SearchAsync(new CenterSearchDto(null, null, null, 1, 500))).Items
            .Where(c => c.Latitude.HasValue && c.Longitude.HasValue)
            .Select(c => new
            {
                Center = c,
                Distance = HaversineKm(latitude, longitude, (double)c.Latitude!.Value, (double)c.Longitude!.Value)
            })
            .Where(x => x.Distance <= radiusKm)
            .OrderBy(x => x.Distance)
            .ToList();

        var output = new List<CenterResponseDto>(centers.Count);
        foreach (var item in centers)
            output.Add(await MapCenter(item.Center, item.Distance));

        return output;
    }

    public async Task<CenterResponseDto> UpdateConfigurationAsync(Guid centerId, CenterConfigurationDto dto, Guid adminId)
    {
        await EnsureAdminAuthority(centerId, adminId);

        var slot = ClampSlotDuration(dto.SlotDurationMinutes);
        var advance = Math.Clamp(dto.AdvanceBookingDays, 1, 90);

        var warnings = new List<string>();
        if (slot != dto.SlotDurationMinutes)
            warnings.Add($"Slot duration adjusted to {slot} minutes.");
        if (advance != dto.AdvanceBookingDays)
            warnings.Add("Maximum advance booking is 90 days. Value adjusted to 90");
        var warning = warnings.Count > 0 ? string.Join(" ", warnings) : null;

        var updateModel = new CenterConfigurationDto(slot, advance, Math.Max(dto.CancellationHours, 0), dto.AutoApproveAppointments, dto.WorkingHours, dto.RequiresPaymentBeforeConfirmation, dto.ServicesOffered);
        try
        {
            await centerRepository.UpdateConfigurationAsync(centerId, updateModel);
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not NotFoundException)
        {
            logger.LogError(ex, "Failed to save configuration for center {CenterId}", centerId);
            throw new ApplicationException("Settings not saved. Try again or contact support");
        }

        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        await auditLogger.LogAsync(AuditActions.CenterConfigChanged, adminId, "Admin", centerId, "HealthcareCenter", centerId);
        var response = await MapCenter(center, null);
        return response with { Warning = warning };
    }

    public async Task<AdminCenterConfigDto> GetAdminConfigAsync(Guid centerId, Guid adminId)
    {
        await EnsureAdminAuthority(centerId, adminId);
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        return MapAdminConfig(center);
    }

    public async Task<AdminCenterConfigDto> UpdateAdminConfigAsync(Guid centerId, AdminCenterConfigUpdateDto dto, Guid adminId)
    {
        await EnsureAdminAuthority(centerId, adminId);
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        center.UpdateGeneralInfo(dto.Name, dto.Phone, dto.Email, dto.City, dto.Region, dto.FullAddress, dto.Services, dto.Specializations);
        try
        {
            await centerRepository.UpdateAsync(center);
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not NotFoundException)
        {
            logger.LogError(ex, "Failed to save general config for center {CenterId}", centerId);
            throw new ApplicationException("Settings not saved. Try again or contact support");
        }
        await auditLogger.LogAsync(AuditActions.CenterConfigChanged, adminId, "Admin", centerId, "HealthcareCenter", centerId);
        return MapAdminConfig(center);
    }

    public async Task<AdminBookingRulesDto> GetBookingRulesAsync(Guid centerId, Guid adminId)
    {
        await EnsureAdminAuthority(centerId, adminId);
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        return MapBookingRules(center);
    }

    public async Task<AdminBookingRulesDto> UpdateBookingRulesAsync(Guid centerId, AdminBookingRulesDto dto, Guid adminId)
    {
        await EnsureAdminAuthority(centerId, adminId);

        var slot = ClampSlotDuration(dto.SlotDurationMinutes);
        var advance = Math.Clamp(dto.AdvanceBookingDays, 1, 90);

        var warnings = new List<string>();
        if (slot != dto.SlotDurationMinutes)
            warnings.Add($"Slot duration adjusted to {slot} minutes.");
        if (advance != dto.AdvanceBookingDays)
            warnings.Add("Maximum advance booking is 90 days. Value adjusted to 90");
        var warning = warnings.Count > 0 ? string.Join(" ", warnings) : null;

        var configDto = new CenterConfigurationDto(slot, advance, Math.Max(dto.CancellationHoursMin, 0), dto.AutoApproveAll || dto.AutoApproveKnownPatients, null, dto.RequiresPaymentBeforeConfirmation);
        try
        {
            await centerRepository.UpdateConfigurationAsync(centerId, configDto);
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not NotFoundException)
        {
            logger.LogError(ex, "Failed to save booking rules for center {CenterId}", centerId);
            throw new ApplicationException("Settings not saved. Try again or contact support");
        }
        await auditLogger.LogAsync(AuditActions.CenterConfigChanged, adminId, "Admin", centerId, "HealthcareCenter", centerId);
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        return MapBookingRules(center, warning);
    }

    public async Task<AdminWorkingHoursDto> GetWorkingHoursAsync(Guid centerId, Guid adminId)
    {
        await EnsureAdminAuthority(centerId, adminId);
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        return WorkingHoursConverter.ToAdminDto(center.WorkingHours);
    }

    public async Task<AdminWorkingHoursDto> UpdateWorkingHoursAsync(Guid centerId, AdminWorkingHoursDto dto, Guid adminId)
    {
        await EnsureAdminAuthority(centerId, adminId);
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        var newHours = WorkingHoursConverter.FromAdminDto(dto);
        var configDto = new CenterConfigurationDto(
            center.SlotDurationMinutes, center.AdvanceBookingDays, center.CancellationHours,
            center.AutoApproveAppointments, newHours, center.RequiresPaymentBeforeConfirmation);
        try
        {
            await centerRepository.UpdateConfigurationAsync(centerId, configDto);
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not NotFoundException)
        {
            logger.LogError(ex, "Failed to save working hours for center {CenterId}", centerId);
            throw new ApplicationException("Settings not saved. Try again or contact support");
        }
        await auditLogger.LogAsync(AuditActions.CenterConfigChanged, adminId, "Admin", centerId, "HealthcareCenter", centerId);
        return WorkingHoursConverter.ToAdminDto(newHours);
    }

    private static AdminCenterConfigDto MapAdminConfig(HealthcareCenter c) =>
        new(c.Id.ToString(), c.CenterName, c.PhoneNumber, c.Email, c.City, c.Region, c.Address,
            c.LicenseNumber, (double?)c.Latitude, (double?)c.Longitude,
            c.ServicesOffered, c.Specializations, c.ProfileImageUrl);

    private static AdminBookingRulesDto MapBookingRules(HealthcareCenter c, string? warning = null) =>
        new(c.SlotDurationMinutes, c.AdvanceBookingDays, c.CancellationHours,
            c.AutoApproveAppointments, false, c.RequiresPaymentBeforeConfirmation, warning);

    public async Task<DoctorCenterRelationDto> AddDoctorToCenterAsync(Guid centerId, AddDoctorDto dto, Guid adminId)
    {
        await EnsureAdminAuthority(centerId, adminId);

        var doctor = await doctorRepository.GetByIdAsync(dto.DoctorId)
            ?? throw new NotFoundException(nameof(Doctor), dto.DoctorId);

        var existing = await centerRepository.GetDoctorsAsync(centerId);
        if (existing.Any(x => x.DoctorId == doctor.Id && x.IsActive))
            throw new DomainException("Doctor is already affiliated with this center.");

        var relation = new DoctorHealthcareCenter(doctor.Id, centerId, dto.ConsultationFee);
        await centerRepository.AddDoctorAsync(relation);
        await unitOfWork.SaveChangesAsync();

        logger.LogInformation("Doctor invitation email stub sent to {Email} for center {CenterId}", doctor.Email, centerId);

        return new DoctorCenterRelationDto(doctor.Id, centerId, relation.ConsultationFee, relation.IsActive, relation.JoinedDate);
    }

    public async Task<DoctorCenterRelationDto> UpdateConsultationFeeAsync(Guid centerId, Guid doctorId, UpdateConsultationFeeDto dto, Guid adminId, CancellationToken ct = default)
    {
        await EnsureAdminAuthority(centerId, adminId);

        var relations = await centerRepository.GetDoctorsAsync(centerId);
        var relation = relations.FirstOrDefault(x => x.DoctorId == doctorId && x.IsActive)
            ?? throw new NotFoundException(nameof(DoctorHealthcareCenter), doctorId);

        relation.UpdateFee(dto.ConsultationFee);
        await unitOfWork.SaveChangesAsync(ct);

        await auditLogger.LogAsync(AuditActions.CenterConfigChanged, adminId, "Admin", centerId, "DoctorHealthcareCenter", relation.Id);
        return new DoctorCenterRelationDto(relation.DoctorId, relation.CenterId, relation.ConsultationFee, relation.IsActive, relation.JoinedDate);
    }

    public async Task<bool> RemoveDoctorFromCenterAsync(Guid centerId, Guid doctorId, Guid adminId)
    {
        await EnsureAdminAuthority(centerId, adminId);
        var removed = await centerRepository.RemoveDoctorAsync(doctorId, centerId);
        if (removed)
            await unitOfWork.SaveChangesAsync();
        return removed;
    }

    public async Task<IEnumerable<DoctorResponseDto>> GetDoctorsAsync(Guid centerId)
    {
        var doctors = await doctorRepository.GetByCenterAsync(centerId);
        var results = new List<DoctorResponseDto>();

        foreach (var doctor in doctors)
        {
            var fee = doctor.DoctorHealthcareCenters.FirstOrDefault(x => x.CenterId == centerId && x.IsActive)?.ConsultationFee;
            var nextDate = DateOnly.FromDateTime(DateTime.UtcNow);
            DateTime? nextSlot = null;
            for (var i = 0; i < 14 && nextSlot is null; i++)
            {
                var date = nextDate.AddDays(i);
                var slots = await appointmentAvailabilityService.GetAvailableSlotsAsync(doctor.Id, centerId, date);
                var first = slots.FirstOrDefault(x => x.IsAvailable);
                if (first != default)
                    nextSlot = date.ToDateTime(first.Time);
            }

            var centers = doctor.DoctorHealthcareCenters
                .Where(x => x.IsActive)
                .Select(x => new DoctorCenterInfoDto(x.CenterId, x.Center?.CenterName ?? string.Empty, x.ConsultationFee))
                .ToList();

            results.Add(new DoctorResponseDto(
                doctor.Id,
                doctor.FullName,
                doctor.Specialization,
                doctor.LicenseNumber,
                doctor.YearsOfExperience,
                doctor.Qualifications,
                doctor.LanguagesSpoken,
                fee,
                nextSlot,
                centers));
        }

        return results;
    }

    public async Task<IEnumerable<AdminDoctorRosterItemDto>> GetAdminDoctorRosterAsync(Guid centerId, Guid adminId, CancellationToken ct = default)
    {
        await EnsureAdminAuthority(centerId, adminId);
        var relations = await centerRepository.GetDoctorsAsync(centerId);
        return relations
            .Select(r => new AdminDoctorRosterItemDto(
                r.DoctorId,
                r.Doctor.FullName,
                r.Doctor.Specialization,
                r.Doctor.LicenseNumber,
                r.Doctor.LicenseVerified,
                r.ConsultationFee,
                r.JoinedDate,
                r.IsActive,
                r.Doctor.ProfileImageUrl,
                0))
            .ToList();
    }

    public async Task<SubscriptionPaymentInitiationDto> InitiateSubscriptionPaymentAsync(
        Guid centerId, Guid planId, SubscriptionBillingCycle billingCycle, Guid adminId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetWithAdminsAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        if (!center.Admins.Any(a => a.Id == adminId))
            throw new ForbiddenException("You do not have authority over this healthcare center.");

        var plan = await subscriptionPlanService.GetPlanByIdAsync(planId, ct);
        if (!plan.IsActive)
            throw new DomainException("The selected subscription plan is not currently available.");

        var baseAmount = billingCycle == SubscriptionBillingCycle.Monthly ? plan.MonthlyPrice : plan.YearlyPrice;
        if (baseAmount <= 0)
            throw new DomainException("Selected plan has no price configured for this billing cycle.");

        var admin = await userRepository.GetByIdAsync(adminId)
            ?? throw new NotFoundException("Admin", adminId);

        var paymentRef = $"SUB-{Guid.NewGuid():N}";
        while (await paymentRepository.ExistsByRefAsync(paymentRef, ct))
            paymentRef = $"SUB-{Guid.NewGuid():N}";

        var amounts = PaymentHelper.CalculatePrice(baseAmount, paymentConfiguration.VatPercent, paymentConfiguration.ServicePercent);

        var payment = Payment.InitiateSubscription(
            centerId, adminId,
            amounts.BaseAmount, amounts.VatPercent, amounts.VatFee,
            amounts.ServicePercent, amounts.ServiceFee, amounts.TotalAmount,
            paymentRef);

        await paymentRepository.CreateAsync(payment);

        payment.Activities.Add(PaymentActivity.Create(
            payment.Id, PaymentAction.Charge, PaymentStatus.Pending,
            paymentRef, amounts.TotalAmount,
            gatewayMessage: $"Subscription payment initiated for plan '{plan.Name}' ({billingCycle})"));

        var nameParts = admin.FullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chapaResponse = await chapaClient.InitializePaymentAsync(new ChapaInitializeRequest(
            amounts.TotalAmount,
            "ETB",
            admin.Email,
            nameParts.FirstOrDefault() ?? admin.FullName,
            nameParts.ElementAtOrDefault(1) ?? "",
            paymentRef,
            chapaConfiguration.CallbackUrl,
            chapaConfiguration.ReturnUrl,
            new ChapaCustomization($"MediMind — {plan.Name} Plan", $"Subscription ({billingCycle}) for {center.CenterName}")), ct);

        var checkoutUrl = chapaResponse?.Data?.CheckoutUrl
            ?? throw new DomainException("Chapa payment initialization failed. Please try again.");

        payment.SetCheckoutUrl(checkoutUrl);
        center.MarkPaymentPending(planId, paymentRef, billingCycle);

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Subscription payment {Ref} initiated for center {CenterId}, plan {PlanId}", paymentRef, centerId, planId);

        return new SubscriptionPaymentInitiationDto(
            payment.Id,
            paymentRef,
            amounts.TotalAmount,
            "ETB",
            checkoutUrl,
            plan.Name,
            billingCycle.ToString(),
            DateTime.UtcNow.AddMinutes(30));
    }

    private async Task EnsureAdminAuthority(Guid centerId, Guid adminId)
    {
        var center = await centerRepository.GetWithAdminsAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        if (!center.Admins.Any(a => a.Id == adminId))
            throw new UnauthorizedException();
    }

    private async Task<CenterResponseDto> MapCenter(HealthcareCenter center, double? distance)
    {
        var workingHours = new WorkingHoursDto(
            center.WorkingHours.GetValueOrDefault("Monday"),
            center.WorkingHours.GetValueOrDefault("Tuesday"),
            center.WorkingHours.GetValueOrDefault("Wednesday"),
            center.WorkingHours.GetValueOrDefault("Thursday"),
            center.WorkingHours.GetValueOrDefault("Friday"),
            center.WorkingHours.GetValueOrDefault("Saturday"),
            center.WorkingHours.GetValueOrDefault("Sunday"));

        var isOpen = WorkingHoursParser.IsOpen(workingHours, DateTime.UtcNow.DayOfWeek);
        var doctorCount = (await centerRepository.GetDoctorsAsync(center.Id)).Count(x => x.IsActive);

        return new CenterResponseDto(
            center.Id,
            center.CenterName,
            center.CenterType,
            center.LicenseNumber,
            center.Address,
            center.City,
            center.Region,
            center.PhoneNumber,
            center.Email,
            workingHours,
            center.ServicesOffered,
            center.Specializations,
            center.SubscriptionStatus.ToString(),
            center.SlotDurationMinutes,
            center.AdvanceBookingDays,
            center.CancellationHours,
            center.AutoApproveAppointments,
            doctorCount,
            distance,
            isOpen,
            Latitude: center.Latitude.HasValue ? Convert.ToDouble(center.Latitude.Value) : null,
            Longitude: center.Longitude.HasValue ? Convert.ToDouble(center.Longitude.Value) : null);
    }

    private static Dictionary<string, string> BuildWorkingHoursDictionary(WorkingHoursDto dto)
    {
        return new Dictionary<string, string>()
        {
            ["Monday"] = dto.Monday ?? string.Empty,
            ["Tuesday"] = dto.Tuesday ?? string.Empty,
            ["Wednesday"] = dto.Wednesday ?? string.Empty,
            ["Thursday"] = dto.Thursday ?? string.Empty,
            ["Friday"] = dto.Friday ?? string.Empty,
            ["Saturday"] = dto.Saturday ?? string.Empty,
            ["Sunday"] = dto.Sunday ?? string.Empty
        };
    }

    private static int ClampSlotDuration(int duration)
    {
        var allowed = new[] { 15, 30, 45, 60 };
        if (allowed.Contains(duration))
            return duration;
        return duration < 15 ? 15 : duration < 30 ? 30 : duration < 45 ? 45 : 60;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Pow(Math.Sin(dLon / 2), 2);
        var c = 2 * Math.Asin(Math.Sqrt(a));
        return r * c;
    }

    private static double DegreesToRadians(double value) => value * (Math.PI / 180);
}

public class AnalyticsService(
    IHealthcareCenterRepository centerRepository,
    IAppointmentRepository appointmentRepository,
    IQueueRepository queueRepository,
    IPaymentRepository paymentRepository) : IAnalyticsService
{
    public async Task<AnalyticsDashboardDto> GetDashboardAnalyticsAsync(Guid centerId, Guid adminId, DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
            throw new DomainException("Start date must be on or before end date.");

        var center = await centerRepository.GetWithAdminsAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        if (!center.Admins.Any(a => a.Id == adminId))
            throw new UnauthorizedException();

        var filter = new AppointmentFilterDto(null, startDate, endDate, null, 1, 5000);
        var appointments = (await appointmentRepository.GetByCenterAsync(centerId, filter)).Items;

        var volume = appointments
            .GroupBy(a => a.AppointmentDate)
            .Select(g => new PatientVolumePointDto(g.Key, g.Count()))
            .OrderBy(x => x.Date)
            .ToList();

        var noShowByDoctor = appointments
            .GroupBy(a => new { a.DoctorId, DoctorName = a.Doctor.FullName })
            .Select(g =>
            {
                var totalConfirmed = g.Count(x => x.Status == AppointmentStatus.Confirmed || x.Status == AppointmentStatus.Completed || x.Status == AppointmentStatus.NoShow);
                var noShow = g.Count(x => x.Status == AppointmentStatus.NoShow);
                var rate = totalConfirmed == 0 ? 0 : (double)noShow / totalConfirmed * 100;
                return new NoShowRateDoctorDto(g.Key.DoctorId, g.Key.DoctorName, Math.Round(rate, 2), totalConfirmed);
            })
            .OrderByDescending(x => x.NoShowRate)
            .ToList();

        var peak = appointments
            .GroupBy(a => new { Day = (int)a.AppointmentDate.DayOfWeek, Hour = a.AppointmentTime.Hour })
            .Select(g => new PeakHourPointDto(g.Key.Day, g.Key.Hour, g.Count()))
            .OrderBy(x => x.DayOfWeek).ThenBy(x => x.Hour)
            .ToList();

        var doctorUtilization = appointments
            .GroupBy(a => new { a.DoctorId, a.Doctor.FullName })
            .Select(g =>
            {
                var slotsUsed = g.Count(x => x.Status is AppointmentStatus.Confirmed or AppointmentStatus.Completed or AppointmentStatus.InProgress or AppointmentStatus.NoShow);
                var totalSlots = Math.Max(slotsUsed, g.Count() + 1);
                var utilization = totalSlots == 0 ? 0 : (double)slotsUsed / totalSlots * 100;
                return new DoctorUtilizationDto(g.Key.DoctorId, g.Key.FullName, Math.Round(utilization, 2), slotsUsed, totalSlots);
            })
            .ToList();

        var queues = new List<QueueEntry>();
        for (var d = startDate; d <= endDate; d = d.AddDays(1))
            queues.AddRange(await queueRepository.GetCenterQueueAsync(centerId, d));

        var waitSamples = queues
            .Where(q => q.CalledTime.HasValue && q.ConsultationStartTime.HasValue)
            .Select(q => (q.ConsultationStartTime!.Value - q.CalledTime!.Value).TotalMinutes)
            .ToList();
        var avgWait = waitSamples.Count == 0 ? (double?)null : Math.Round(waitSamples.Average(), 2);

        var paymentByAppointment = new Dictionary<Guid, IReadOnlyList<Payment>>();
        foreach (var appt in appointments)
            paymentByAppointment[appt.Id] = await paymentRepository.GetByAppointmentAsync(appt.Id);

        var payments = paymentByAppointment.Values
            .SelectMany(x => x)
            .Where(p => p.Status == PaymentStatus.Completed)
            .ToList();

        var revenueByDoctor = appointments
            .SelectMany(a => paymentByAppointment[a.Id].Select(p => new { a.DoctorId, Payment = p }))
            .Where(x => x.Payment.Status == PaymentStatus.Completed)
            .GroupBy(x => x.DoctorId)
            .Select(g => new RevenueByDoctorDto(g.Key, g.Sum(x => x.Payment.Amount)))
            .ToList();

        var totalRevenue = payments.Sum(x => x.Amount);
        var avgPerAppointment = appointments.Count == 0 ? 0 : totalRevenue / appointments.Count;

        var summary = new AnalyticsSummaryDto(
            appointments.Count,
            appointments.Count(x => x.Status == AppointmentStatus.Completed),
            appointments.Count(x => x.Status == AppointmentStatus.Cancelled),
            appointments.Count(x => x.Status == AppointmentStatus.NoShow),
            appointments.Select(x => x.PatientId).Distinct().Count());

        return new AnalyticsDashboardDto(
            new AnalyticsPeriodDto(startDate, endDate),
            volume,
            noShowByDoctor,
            peak,
            doctorUtilization,
            avgWait,
            new RevenueMetricsDto(totalRevenue, revenueByDoctor, avgPerAppointment),
            summary,
            appointments.Count == 0 ? "No appointments match selected criteria" : null);
    }
}
