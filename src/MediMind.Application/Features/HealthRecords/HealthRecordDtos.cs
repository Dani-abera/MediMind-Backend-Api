namespace MediMind.Application.Features.HealthRecords;

public record CreateHealthRecordDto(
    DateOnly RecordDate,
    TimeOnly? RecordTime,
    int? SystolicBp,
    int? DiastolicBp,
    decimal? GlucoseLevel,
    decimal? Weight,
    decimal? Height,
    decimal? Temperature,
    int? HeartRate,
    int? OxygenSaturation,
    int? RespiratoryRate,
    string? Notes,
    string? RecordedBy);

public record UpdateHealthRecordDto(
    DateOnly RecordDate,
    TimeOnly? RecordTime,
    int? SystolicBp,
    int? DiastolicBp,
    decimal? GlucoseLevel,
    decimal? Weight,
    decimal? Height,
    decimal? Temperature,
    int? HeartRate,
    int? OxygenSaturation,
    int? RespiratoryRate,
    string? Notes,
    string? RecordedBy);

public record HealthRecordResponseDto(
    Guid RecordId,
    Guid PatientId,
    DateOnly RecordDate,
    TimeOnly RecordTime,
    int? SystolicBp,
    int? DiastolicBp,
    decimal? GlucoseLevel,
    decimal? Weight,
    decimal? Height,
    decimal? Temperature,
    int? HeartRate,
    int? OxygenSaturation,
    int? RespiratoryRate,
    string? Notes,
    string RecordedBy,
    DateTime CreatedAt,
    decimal? Bmi);

public record HealthRecordSummaryDto(
    Guid RecordId,
    DateOnly RecordDate,
    int? SystolicBp,
    int? DiastolicBp,
    decimal? GlucoseLevel,
    decimal? Weight,
    decimal? Temperature,
    DateTime CreatedAt);

public record PaginatedHealthRecordsDto(
    IEnumerable<HealthRecordSummaryDto> Data,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

