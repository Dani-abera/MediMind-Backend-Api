namespace MediMind.Domain.Common.Interfaces;

public record HealthTrendDto(
    string Period,
    double? AverageSystolicBp,
    double? AverageDiastolicBp,
    double? AverageGlucose,
    double? AverageWeight,
    int? MinSystolicBp,
    int? MaxSystolicBp,
    int DataPointsCount,
    string TrendDirection);

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
