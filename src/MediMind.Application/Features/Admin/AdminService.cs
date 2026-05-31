using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MediMind.Application.Features.Admin;

// ─── Admin Management ─────────────────────────────────────────────────────────

public class AdminManagementService(
    IHealthcareCenterRepository centerRepository,
    IUserRepository userRepository,
    IEmailService emailService,
    IPasswordService passwordService,
    IUnitOfWork unitOfWork,
    ILogger<AdminManagementService> logger) : IAdminManagementService
{
    public async Task<IReadOnlyList<AdminSummaryDto>> ListAdminsAsync(Guid centerId, Guid requesterId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetWithAdminsAsync(centerId, ct)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        EnsureAdminBelongsToCenter(center, requesterId);

        return center.Admins
            .Select(a => new AdminSummaryDto(a.Id, a.FullName, a.Email, a.PhoneNumber, a.Role, a.IsActive, a.LastLogin))
            .ToList();
    }

    public async Task<AdminSummaryDto> InviteAdminAsync(Guid centerId, InviteAdminDto dto, Guid requesterId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetWithAdminsAsync(centerId, ct)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        EnsureAdminBelongsToCenter(center, requesterId);

        if (await userRepository.ExistsByEmailAsync(dto.Email, ct))
            throw new DomainException("An account with this email already exists.");

        var tempPassword = GenerateTemporaryPassword();
        var newAdmin = new HealthcareCenterAdmin(
            dto.Email,
            dto.PhoneNumber,
            dto.FullName,
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Gender.NotMentioned,
            centerId,
            dto.Role);
        newAdmin.SetPasswordHash(passwordService.HashPassword(tempPassword));
        newAdmin.Activate();

        await userRepository.AddAsync(newAdmin, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var invitationLink = $"https://app.medimind.et/admin/setup?token={Guid.NewGuid():N}&email={Uri.EscapeDataString(dto.Email)}";
        await emailService.SendDoctorInvitationAsync(dto.Email, dto.FullName, center.CenterName, invitationLink, ct);

        logger.LogInformation("Admin {Email} invited to center {CenterId} by {RequesterId}", dto.Email, centerId, requesterId);

        return new AdminSummaryDto(newAdmin.Id, newAdmin.FullName, newAdmin.Email, newAdmin.PhoneNumber, newAdmin.Role, newAdmin.IsActive, newAdmin.LastLogin);
    }

    public async Task RemoveAdminAsync(Guid centerId, Guid adminId, Guid requesterId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetWithAdminsAsync(centerId, ct)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        EnsureAdminBelongsToCenter(center, requesterId);

        if (adminId == requesterId)
            throw new DomainException("You cannot remove your own admin account.");

        var activeAdmins = center.Admins.Where(a => a.IsActive).ToList();
        if (activeAdmins.Count <= 1)
            throw new DomainException("Cannot remove the last active admin from a center.");

        var target = center.Admins.FirstOrDefault(a => a.Id == adminId)
            ?? throw new NotFoundException("Admin", adminId);

        target.Deactivate();
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static void EnsureAdminBelongsToCenter(HealthcareCenter center, Guid adminId)
    {
        if (!center.Admins.Any(a => a.Id == adminId))
            throw new TenantIsolationException();
    }

    private static string GenerateTemporaryPassword() =>
        $"Tmp{Guid.NewGuid():N}".Substring(0, 16) + "!1";
}

// ─── Patient Directory ────────────────────────────────────────────────────────

public class PatientDirectoryService(
    IAppointmentRepository appointmentRepository,
    IHealthcareCenterRepository centerRepository,
    IPrescriptionRepository prescriptionRepository,
    IHealthPredictionRepository healthPredictionRepository) : IPatientDirectoryService
{
    public async Task<PagedResult<EnrolledPatientSummaryDto>> ListEnrolledPatientsAsync(
        Guid centerId, PatientDirectoryQueryDto query, Guid requesterId, CancellationToken ct = default)
    {
        await EnsureAdminAccessAsync(centerId, requesterId, ct);

        var filter = new AppointmentFilterDto(null, null, null, null, 1, 10000);
        var allAppointments = (await appointmentRepository.GetByCenterAsync(centerId, filter)).Items
            .ToList();

        var grouped = allAppointments
            .GroupBy(a => a.PatientId)
            .Select(g =>
            {
                var p = g.First().Patient;
                var lastVisit = g.Max(a => a.AppointmentDate);
                return new
                {
                    Patient = p,
                    LastVisit = lastVisit,
                    Total = g.Count()
                };
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            grouped = grouped.Where(x =>
                x.Patient.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Patient.PhoneNumber.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var total = grouped.Count;
        var items = grouped
            .OrderByDescending(x => x.LastVisit)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new EnrolledPatientSummaryDto(
                x.Patient.Id,
                x.Patient.FullName,
                x.Patient.PhoneNumber,
                x.LastVisit == DateOnly.MinValue ? null : x.LastVisit,
                x.Total))
            .ToList();

        return new PagedResult<EnrolledPatientSummaryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<EnrolledPatientDetailDto> GetEnrolledPatientAsync(
        Guid centerId, Guid patientId, Guid requesterId, CancellationToken ct = default)
    {
        await EnsureAdminAccessAsync(centerId, requesterId, ct);

        var filter = new AppointmentFilterDto(null, null, null, null, 1, 10000);
        var appointments = (await appointmentRepository.GetByCenterAsync(centerId, filter)).Items
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.AppointmentDate)
            .ToList();

        if (appointments.Count == 0)
            throw new ForbiddenException("Patient not registered at your healthcare center. Cannot access records");

        var patient = appointments.First().Patient;
        var recent = appointments.Take(5)
            .Select(a => new AppointmentSummaryDto(
                a.Id,
                a.AppointmentDate,
                a.AppointmentTime,
                a.Doctor.FullName,
                a.Status.ToString()))
            .ToList();

        var latestPrediction = await healthPredictionRepository.GetLatestAsync(patientId);
        var prescriptions = await prescriptionRepository.GetByPatientAsync(patientId, 1, 5, ct);

        PatientPredictionSummaryDto? predictionDto = latestPrediction is null ? null : new PatientPredictionSummaryDto(
            latestPrediction.Id,
            latestPrediction.PredictionDate,
            latestPrediction.DiabetesRisk,
            latestPrediction.DiabetesCategory.ToString(),
            latestPrediction.HypertensionRisk,
            latestPrediction.HypertensionCategory.ToString(),
            latestPrediction.CvdRisk,
            latestPrediction.CvdCategory.ToString(),
            latestPrediction.Confidence,
            latestPrediction.Recommendations);

        var prescriptionDtos = prescriptions
            .Where(p => p.CenterId == centerId)
            .Select(p => new PatientPrescriptionSummaryDto(
                p.Id,
                p.Doctor.FullName,
                p.IssueDate,
                p.Diagnosis,
                p.Status.ToString()))
            .ToList();

        return new EnrolledPatientDetailDto(
            patient.Id,
            patient.FullName,
            patient.Email,
            patient.PhoneNumber,
            patient.DateOfBirth,
            patient.Gender.ToString(),
            patient.Address,
            patient.BloodType?.ToString(),
            patient.Allergies,
            appointments.Count,
            appointments.Max(a => a.AppointmentDate),
            appointments.Min(a => a.AppointmentDate),
            recent,
            predictionDto,
            prescriptionDtos);
    }

    private async Task EnsureAdminAccessAsync(Guid centerId, Guid adminId, CancellationToken ct)
    {
        var center = await centerRepository.GetWithAdminsAsync(centerId, ct)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        if (!center.Admins.Any(a => a.Id == adminId))
            throw new TenantIsolationException();
    }
}

// ─── Today Dashboard ──────────────────────────────────────────────────────────

public class TodayDashboardService(
    IHealthcareCenterRepository centerRepository,
    IAppointmentRepository appointmentRepository,
    IQueueRepository queueRepository,
    IPaymentRepository paymentRepository,
    IDoctorRepository doctorRepository) : ITodayDashboardService
{
    public async Task<TodayDashboardDto> GetTodayDashboardAsync(Guid centerId, Guid requesterId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetWithAdminsAsync(centerId, ct)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        if (!center.Admins.Any(a => a.Id == requesterId))
            throw new TenantIsolationException();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lastWeekSameDay = today.AddDays(-7);

        var todayAppointments = await appointmentRepository.GetByCenterAndDateAsync(centerId, today, ct);
        var lastWeekAppointments = await appointmentRepository.GetByCenterAndDateAsync(centerId, lastWeekSameDay, ct);
        var todayQueue = await queueRepository.GetByCenterAndDateAsync(centerId, today, ct);

        var todayPayments = new List<Domain.Entities.Payment>();
        foreach (var appt in todayAppointments)
        {
            var payments = await paymentRepository.GetByAppointmentAsync(appt.Id, ct);
            todayPayments.AddRange(payments);
        }

        var allDoctors = await doctorRepository.GetByCenterAsync(centerId, ct);

        var waitSamples = todayQueue
            .Where(q => q.CalledTime.HasValue && q.ConsultationStartTime.HasValue)
            .Select(q => (q.ConsultationStartTime!.Value - q.CalledTime!.Value).TotalMinutes)
            .ToList();
        var avgWait = waitSamples.Count > 0 ? waitSamples.Average() : 0;

        var todayCount = todayAppointments.Count();
        var lastWeekCount = lastWeekAppointments.Count();
        var changePercent = lastWeekCount == 0 ? 0.0
            : Math.Round((double)(todayCount - lastWeekCount) / lastWeekCount * 100, 2);

        return new TodayDashboardDto(
            new TodayAppointmentsDto(
                todayCount,
                todayAppointments.Count(a => a.Status == AppointmentStatus.Pending),
                todayAppointments.Count(a => a.Status == AppointmentStatus.Confirmed),
                todayAppointments.Count(a => a.Status == AppointmentStatus.Completed),
                todayAppointments.Count(a => a.Status == AppointmentStatus.Cancelled),
                todayAppointments.Count(a => a.Status == AppointmentStatus.NoShow)),
            new TodayQueueDto(
                todayQueue.Count(q => q.Status == QueueStatus.Waiting || q.Status == QueueStatus.Called),
                todayQueue.Count(q => q.Status == QueueStatus.InConsultation),
                Math.Round(avgWait, 2)),
            new TodayRevenueDto(
                todayPayments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
                todayPayments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount)),
            new TodayDoctorsDto(
                todayAppointments.Select(a => a.DoctorId).Distinct().Count(),
                allDoctors.Count),
            new PatientVolumeComparisonDto(todayCount, lastWeekCount, changePercent));
    }
}

// ─── Revenue Service ──────────────────────────────────────────────────────────

public class RevenueService(
    IHealthcareCenterRepository centerRepository,
    IAppointmentRepository appointmentRepository,
    IPaymentRepository paymentRepository) : IRevenueService
{
    public async Task<RevenueReportDto> GetRevenueReportAsync(
        Guid centerId, RevenueQueryDto query, Guid requesterId, CancellationToken ct = default)
    {
        await EnsureAdminAccessAsync(centerId, requesterId, ct);

        var filter = new AppointmentFilterDto(null, query.StartDate, query.EndDate, null, 1, 5000);
        var appointments = (await appointmentRepository.GetByCenterAsync(centerId, filter)).Items;

        var paymentsByAppt = new Dictionary<Guid, IReadOnlyList<Domain.Entities.Payment>>();
        foreach (var appt in appointments)
            paymentsByAppt[appt.Id] = await paymentRepository.GetByAppointmentAsync(appt.Id, ct);

        var allPayments = paymentsByAppt.Values.SelectMany(x => x).ToList();
        var totalCollected = allPayments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
        var totalPending = allPayments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount);

        var groups = BuildGroups(query.GroupBy, appointments, paymentsByAppt, query.StartDate, query.EndDate);

        return new RevenueReportDto(query.StartDate, query.EndDate, totalCollected, totalPending, groups);
    }

    public async Task<IEnumerable<RevenueCsvRowDto>> GetRevenueCsvRowsAsync(
        Guid centerId, DateOnly startDate, DateOnly endDate, Guid requesterId, CancellationToken ct = default)
    {
        await EnsureAdminAccessAsync(centerId, requesterId, ct);

        var filter = new AppointmentFilterDto(null, startDate, endDate, null, 1, 5000);
        var appointments = (await appointmentRepository.GetByCenterAsync(centerId, filter)).Items;

        var rows = new List<RevenueCsvRowDto>();
        foreach (var appt in appointments)
        {
            var payments = await paymentRepository.GetByAppointmentAsync(appt.Id, ct);
            foreach (var payment in payments)
            {
                rows.Add(new RevenueCsvRowDto(
                    appt.AppointmentDate.ToString("yyyy-MM-dd"),
                    appt.Id,
                    appt.Doctor.FullName,
                    appt.Patient.FullName,
                    payment.Amount,
                    payment.PaymentMethod,
                    payment.Status.ToString()));
            }
        }

        return rows;
    }

    private static List<RevenueGroupDto> BuildGroups(
        string groupBy,
        IReadOnlyList<Domain.Entities.Appointment> appointments,
        Dictionary<Guid, IReadOnlyList<Domain.Entities.Payment>> paymentsByAppt,
        DateOnly startDate,
        DateOnly endDate)
    {
        return groupBy.ToLowerInvariant() switch
        {
            "doctor" => appointments
                .GroupBy(a => new { a.DoctorId, a.Doctor.FullName })
                .Select(g => new RevenueGroupDto(
                    g.Key.FullName,
                    g.SelectMany(a => paymentsByAppt[a.Id]).Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
                    g.SelectMany(a => paymentsByAppt[a.Id]).Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
                    g.Count()))
                .ToList(),
            "week" => GroupByPeriod(appointments, paymentsByAppt, d => GetWeekLabel(d)),
            "month" => GroupByPeriod(appointments, paymentsByAppt, d => d.ToString("yyyy-MM")),
            _ => GroupByPeriod(appointments, paymentsByAppt, d => d.ToString("yyyy-MM-dd"))
        };
    }

    private static List<RevenueGroupDto> GroupByPeriod(
        IReadOnlyList<Domain.Entities.Appointment> appointments,
        Dictionary<Guid, IReadOnlyList<Domain.Entities.Payment>> paymentsByAppt,
        Func<DateOnly, string> labelFn) =>
        appointments
            .GroupBy(a => labelFn(a.AppointmentDate))
            .OrderBy(g => g.Key)
            .Select(g => new RevenueGroupDto(
                g.Key,
                g.SelectMany(a => paymentsByAppt[a.Id]).Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
                g.SelectMany(a => paymentsByAppt[a.Id]).Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
                g.Count()))
            .ToList();

    private static string GetWeekLabel(DateOnly date)
    {
        var startOfWeek = date.AddDays(-(int)date.DayOfWeek);
        return $"W{startOfWeek:yyyy-MM-dd}";
    }

    private async Task EnsureAdminAccessAsync(Guid centerId, Guid adminId, CancellationToken ct)
    {
        var center = await centerRepository.GetWithAdminsAsync(centerId, ct)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        if (!center.Admins.Any(a => a.Id == adminId))
            throw new TenantIsolationException();
    }
}

// ─── Bulk Operations ──────────────────────────────────────────────────────────

public class BulkOperationsService(
    IAppointmentRepository appointmentRepository,
    IQueueRepository queueRepository,
    INotificationService notificationService,
    IUnitOfWork unitOfWork) : IBulkOperationsService
{
    public async Task<BulkApproveResultDto> BulkApproveAppointmentsAsync(
        BulkApproveDto dto, Guid requesterId, Guid centerTenantId, CancellationToken ct = default)
    {
        var approved = new List<Guid>();
        var failed = new List<BulkApproveFailureDto>();

        foreach (var id in dto.AppointmentIds.Distinct())
        {
            try
            {
                var appt = await appointmentRepository.GetByIdAsync(id)
                    ?? throw new NotFoundException(nameof(Appointment), id);

                if (appt.CenterId != centerTenantId)
                {
                    failed.Add(new BulkApproveFailureDto(id, "Appointment does not belong to your center."));
                    continue;
                }

                if (appt.Status != AppointmentStatus.Pending)
                {
                    failed.Add(new BulkApproveFailureDto(id, $"Cannot approve appointment with status {appt.Status}."));
                    continue;
                }

                await appointmentRepository.UpdateStatusAsync(id, AppointmentStatus.Confirmed, requesterId);
                approved.Add(id);
            }
            catch (NotFoundException)
            {
                failed.Add(new BulkApproveFailureDto(id, "Appointment not found."));
            }
        }

        if (approved.Count > 0)
            await unitOfWork.SaveChangesAsync(ct);

        return new BulkApproveResultDto(approved, failed);
    }

    public async Task SkipQueueEntryAsync(Guid queueId, Guid requesterId, Guid centerTenantId, CancellationToken ct = default)
    {
        var entry = await queueRepository.GetByIdAsync(queueId)
            ?? throw new NotFoundException(nameof(QueueEntry), queueId);

        if (entry.CenterId != centerTenantId)
            throw new TenantIsolationException();

        if (entry.Status != QueueStatus.Waiting && entry.Status != QueueStatus.Called)
            throw new DomainException("Only waiting or called patients can be skipped.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var allWaiting = (await queueRepository.GetCenterQueueAsync(centerTenantId, today))
            .Where(q => q.Status == QueueStatus.Waiting || q.Status == QueueStatus.Called)
            .OrderByDescending(q => q.Position)
            .ToList();

        var maxPosition = allWaiting.Count > 0 ? allWaiting.Max(q => q.Position) : entry.Position;
        entry.UpdatePosition(maxPosition + 1, 30);
        await queueRepository.RecalculatePositionsAsync(centerTenantId, today);
        await unitOfWork.SaveChangesAsync(ct);

        try
        {
            var appointment = entry.Appointment;
            if (appointment?.PatientId is Guid patientId)
            {
                await notificationService.SendPushAsync(
                    patientId,
                    "Queue Update",
                    "You've been skipped. Please check in with reception.",
                    new Dictionary<string, string> { ["type"] = "queue_skipped", ["queueId"] = queueId.ToString() },
                    ct);
            }
        }
        catch { /* notifications are best-effort */ }
    }
}

// ─── Audit Log Service ────────────────────────────────────────────────────────

public class AuditLogService(
    IAuditLogRepository auditLogRepository,
    IHealthcareCenterRepository centerRepository) : IAuditLogService
{
    public async Task<PagedResult<AuditLogEntryDto>> GetAuditLogsAsync(
        Guid centerId, AuditLogQueryDto query, Guid requesterId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetWithAdminsAsync(centerId, ct)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        if (!center.Admins.Any(a => a.Id == requesterId))
            throw new TenantIsolationException();

        var filterDto = new AuditLogFilterDto(query.StartDate, query.EndDate, query.UserId, query.Action, query.Page, query.PageSize);
        var result = await auditLogRepository.QueryAsync(centerId, filterDto, ct);
        var items = result.Items.Select(l => new AuditLogEntryDto(
            l.LogId,
            l.Timestamp,
            l.UserId,
            l.UserType,
            l.Action,
            l.EntityType,
            l.EntityId,
            l.IpAddress,
            l.Metadata)).ToList();

        return new PagedResult<AuditLogEntryDto>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<PagedResult<AuditLogEntryDto>> GetGlobalAuditLogsAsync(AuditLogQueryDto query, CancellationToken ct = default)
    {
        var filterDto = new AuditLogFilterDto(query.StartDate, query.EndDate, query.UserId, query.Action, query.Page, query.PageSize);
        var result = await auditLogRepository.QueryGlobalAsync(filterDto, ct);
        var items = result.Items.Select(l => new AuditLogEntryDto(
            l.LogId, l.Timestamp, l.UserId, l.UserType, l.Action,
            l.EntityType, l.EntityId, l.IpAddress, l.Metadata)).ToList();
        return new PagedResult<AuditLogEntryDto>(items, result.Page, result.PageSize, result.TotalCount);
    }
}
