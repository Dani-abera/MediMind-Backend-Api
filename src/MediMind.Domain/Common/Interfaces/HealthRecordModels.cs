namespace MediMind.Domain.Common.Interfaces;

public record TrendPointDto(string Date, double Value);

public record MetricTrendDto(
    string Metric,
    string Unit,
    List<TrendPointDto> Points,
    double Average,
    double Minimum,
    double Maximum,
    string TrendDirection,
    double? NormalMin,
    double? NormalMax,
    string? Insight);

public record HealthTrendsResponseDto(
    MetricTrendDto? BloodPressureSystolic,
    MetricTrendDto? BloodPressureDiastolic,
    MetricTrendDto? GlucoseLevel,
    MetricTrendDto? Weight,
    MetricTrendDto? HeartRate,
    MetricTrendDto? Temperature,
    MetricTrendDto? OxygenSaturation,
    string? OverallInsight);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public enum ReminderType
{
    TwentyFourHours,
    TwoHours
}
