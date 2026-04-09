using MediatR;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Enums;
using MediMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace MediMind.Application.Features.Analytics;


// ─── Analytics DTOs ───────────────────────────────────────────────────────────

public record AnalyticsDashboardDto(
    int TotalAppointmentsToday,
    int ConfirmedAppointments,
    int PendingAppointments,
    int CompletedAppointments,
    int NoShowAppointments,
    double NoShowRatePercent,
    int PatientsInQueue,
    int PatientsConsulted,
    double AverageWaitTimeMinutes,
    List<HourlyVolumeDto> HourlyVolume,
    List<DoctorUtilizationDto> DoctorUtilization,
    decimal TotalRevenueToday);

public record HourlyVolumeDto(int Hour, int AppointmentCount);
public record DoctorUtilizationDto(string DoctorName, string Specialization, int AppointmentsToday, int Completed, int NoShows);

public record WeeklyTrendDto(DateOnly Date, int Total, int Completed, int NoShows, decimal Revenue);

// ═══════════════════════════════════════════════════════════════════════════════
// GET DASHBOARD ANALYTICS (FR-024 — Admin only)
// ═══════════════════════════════════════════════════════════════════════════════

public record GetDashboardAnalyticsQuery(Guid CenterId, DateOnly Date) : IRequest<AnalyticsDashboardDto>;

public class GetDashboardAnalyticsHandler(MediMindDbContext db)
    : IRequestHandler<GetDashboardAnalyticsQuery, AnalyticsDashboardDto>
{
    public async Task<AnalyticsDashboardDto> Handle(
        GetDashboardAnalyticsQuery request, CancellationToken ct)
    {
        // Multi-tenant: all queries scoped to CenterId (tenant_id)
        var appointmentsToday = await db.Appointments
            .Where(a => a.CenterId == request.CenterId && a.AppointmentDate == request.Date)
            .Include(a => a.Doctor)
            .Include(a => a.QueueEntry)
            .Include(a => a.Payments)
            .ToListAsync(ct);

        var total = appointmentsToday.Count;
        var confirmed = appointmentsToday.Count(a => a.Status == AppointmentStatus.Confirmed);
        var pending = appointmentsToday.Count(a => a.Status == AppointmentStatus.Pending);
        var completed = appointmentsToday.Count(a => a.Status == AppointmentStatus.Completed);
        var noShows = appointmentsToday.Count(a => a.Status == AppointmentStatus.NoShow);
        var noShowRate = total > 0 ? (double)noShows / total * 100 : 0;

        var queueToday = await db.QueueEntries
            .Where(q => q.CenterId == request.CenterId && q.QueueDate == request.Date)
            .ToListAsync(ct);

        var patientsInQueue = queueToday.Count(q => q.Status == QueueStatus.Waiting);
        var patientsConsulted = queueToday.Count(q => q.Status == QueueStatus.Completed);
        var avgWait = queueToday.Where(q => q.CalledTime.HasValue && q.ConsultationStartTime.HasValue)
            .Select(q => (q.ConsultationStartTime!.Value - q.CalledTime!.Value).TotalMinutes)
            .DefaultIfEmpty(0)
            .Average();

        // Hourly volume distribution
        var hourlyVolume = appointmentsToday
            .GroupBy(a => a.AppointmentTime.Hour)
            .Select(g => new HourlyVolumeDto(g.Key, g.Count()))
            .OrderBy(h => h.Hour)
            .ToList();

        // Doctor utilization
        var doctorUtilization = appointmentsToday
            .GroupBy(a => a.DoctorId)
            .Select(g => new DoctorUtilizationDto(
                g.First().Doctor?.FullName ?? "Unknown",
                g.First().Doctor?.Specialization ?? string.Empty,
                g.Count(),
                g.Count(a => a.Status == AppointmentStatus.Completed),
                g.Count(a => a.Status == AppointmentStatus.NoShow)))
            .ToList();

        // Revenue today (completed payments only)
        var totalRevenue = appointmentsToday
            .SelectMany(a => a.Payments)
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        return new AnalyticsDashboardDto(
            total, confirmed, pending, completed, noShows,
            Math.Round(noShowRate, 1),
            patientsInQueue, patientsConsulted,
            Math.Round(avgWait, 1),
            hourlyVolume, doctorUtilization, totalRevenue);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// GET WEEKLY TREND (FR-024)
// ═══════════════════════════════════════════════════════════════════════════════

public record GetWeeklyTrendQuery(Guid CenterId, DateOnly StartDate, DateOnly EndDate)
    : IRequest<IReadOnlyList<WeeklyTrendDto>>;

public class GetWeeklyTrendHandler(MediMindDbContext db)
    : IRequestHandler<GetWeeklyTrendQuery, IReadOnlyList<WeeklyTrendDto>>
{
    public async Task<IReadOnlyList<WeeklyTrendDto>> Handle(
        GetWeeklyTrendQuery request, CancellationToken ct)
    {
        var appointments = await db.Appointments
            .Where(a => a.CenterId == request.CenterId &&
                        a.AppointmentDate >= request.StartDate &&
                        a.AppointmentDate <= request.EndDate)
            .Include(a => a.Payments)
            .ToListAsync(ct);

        return appointments
            .GroupBy(a => a.AppointmentDate)
            .Select(g => new WeeklyTrendDto(
                g.Key,
                g.Count(),
                g.Count(a => a.Status == AppointmentStatus.Completed),
                g.Count(a => a.Status == AppointmentStatus.NoShow),
                g.SelectMany(a => a.Payments)
                    .Where(p => p.Status == PaymentStatus.Completed)
                    .Sum(p => p.Amount)))
            .OrderBy(d => d.Date)
            .ToList();
    }
}
