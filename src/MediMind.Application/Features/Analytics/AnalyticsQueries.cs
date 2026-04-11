using MediatR;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Enums;
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

// ═══════════════════════════════════════════════════════════════════════════════
// GET WEEKLY TREND (FR-024)
// ═══════════════════════════════════════════════════════════════════════════════

public record GetWeeklyTrendQuery(Guid CenterId, DateOnly StartDate, DateOnly EndDate)
    : IRequest<IReadOnlyList<WeeklyTrendDto>>;
