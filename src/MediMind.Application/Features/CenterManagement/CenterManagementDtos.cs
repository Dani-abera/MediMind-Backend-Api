namespace MediMind.Application.Features.CenterManagement;

public record WorkingHoursDto(
    string? Monday,
    string? Tuesday,
    string? Wednesday,
    string? Thursday,
    string? Friday,
    string? Saturday,
    string? Sunday);

public static class WorkingHoursParser
{
    public static WorkingHoursDto Parse(string json)
    {
        var dictionary = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(json)
                         ?? new Dictionary<string, string?>();

        return new WorkingHoursDto(
            dictionary.GetValueOrDefault("Monday"),
            dictionary.GetValueOrDefault("Tuesday"),
            dictionary.GetValueOrDefault("Wednesday"),
            dictionary.GetValueOrDefault("Thursday"),
            dictionary.GetValueOrDefault("Friday"),
            dictionary.GetValueOrDefault("Saturday"),
            dictionary.GetValueOrDefault("Sunday"));
    }

    public static bool IsOpen(WorkingHoursDto hours, DayOfWeek day)
    {
        var value = day switch
        {
            DayOfWeek.Monday => hours.Monday,
            DayOfWeek.Tuesday => hours.Tuesday,
            DayOfWeek.Wednesday => hours.Wednesday,
            DayOfWeek.Thursday => hours.Thursday,
            DayOfWeek.Friday => hours.Friday,
            DayOfWeek.Saturday => hours.Saturday,
            _ => hours.Sunday
        };

        return !string.IsNullOrWhiteSpace(value);
    }

    public static (TimeOnly open, TimeOnly close)? GetTodayHours(WorkingHoursDto hours)
    {
        var day = DateTime.UtcNow.DayOfWeek;
        var value = day switch
        {
            DayOfWeek.Monday => hours.Monday,
            DayOfWeek.Tuesday => hours.Tuesday,
            DayOfWeek.Wednesday => hours.Wednesday,
            DayOfWeek.Thursday => hours.Thursday,
            DayOfWeek.Friday => hours.Friday,
            DayOfWeek.Saturday => hours.Saturday,
            _ => hours.Sunday
        };

        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return null;

        return (TimeOnly.Parse(parts[0]), TimeOnly.Parse(parts[1]));
    }
}

public record RegisterCenterDto(
    string CenterName,
    string CenterType,
    string LicenseNumber,
    string Address,
    string City,
    string Region,
    string PhoneNumber,
    string Email,
    WorkingHoursDto WorkingHours,
    List<string> ServicesOffered,
    List<string>? Specializations,
    double? Latitude,
    double? Longitude);

public record CenterResponseDto(
    Guid CenterId,
    string CenterName,
    string CenterType,
    string LicenseNumber,
    string Address,
    string City,
    string Region,
    string PhoneNumber,
    string Email,
    WorkingHoursDto WorkingHours,
    List<string> ServicesOffered,
    List<string> Specializations,
    string SubscriptionStatus,
    int SlotDurationMinutes,
    int AdvanceBookingDays,
    int CancellationHours,
    bool AutoApproveAppointments,
    int DoctorCount,
    double? Distance,
    bool IsOpenNow,
    string? Warning = null);

public record AddDoctorDto(Guid DoctorId, decimal ConsultationFee);

public record DoctorCenterRelationDto(Guid DoctorId, Guid CenterId, decimal ConsultationFee, bool IsActive, DateOnly JoinedDate);

public record DoctorCenterInfoDto(Guid CenterId, string CenterName, decimal ConsultationFee);

public record DoctorResponseDto(
    Guid DoctorId,
    string FullName,
    string Specialization,
    string LicenseNumber,
    int YearsOfExperience,
    string? Qualifications,
    List<string> LanguagesSpoken,
    decimal? ConsultationFee,
    DateTime? NextAvailableSlot,
    List<DoctorCenterInfoDto> WorkingCenters);

public record AnalyticsPeriodDto(DateOnly StartDate, DateOnly EndDate);
public record PatientVolumePointDto(DateOnly Date, int Count);
public record NoShowRateDoctorDto(Guid DoctorId, string DoctorName, double NoShowRate, int Total);
public record PeakHourPointDto(int DayOfWeek, int Hour, int Count);
public record DoctorUtilizationDto(Guid DoctorId, string DoctorName, double UtilizationPercent, int SlotsUsed, int TotalSlots);
public record RevenueByDoctorDto(Guid DoctorId, decimal Revenue);
public record RevenueMetricsDto(decimal TotalRevenue, List<RevenueByDoctorDto> ByDoctor, decimal AveragePerAppointment);
public record AnalyticsSummaryDto(int TotalAppointments, int CompletedCount, int CancelledCount, int NoShowCount, int NewPatientsCount);

public record AnalyticsDashboardDto(
    AnalyticsPeriodDto Period,
    List<PatientVolumePointDto> PatientVolumeTrends,
    List<NoShowRateDoctorDto> NoShowRateByDoctor,
    List<PeakHourPointDto> PeakHoursHeatmap,
    List<DoctorUtilizationDto> DoctorUtilization,
    double? AverageWaitTimeMinutes,
    RevenueMetricsDto RevenueMetrics,
    AnalyticsSummaryDto Summary);
