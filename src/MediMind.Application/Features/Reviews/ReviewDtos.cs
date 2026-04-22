namespace MediMind.Application.Features.Reviews;

public record CreateReviewDto(
    int Rating,
    string? Comment,
    bool ReviewDoctor = true,
    bool ReviewCenter = false);

public record ReviewResponseDto(
    Guid ReviewId,
    Guid PatientId,
    string PatientName,
    Guid? DoctorId,
    Guid? CenterId,
    Guid AppointmentId,
    int Rating,
    string? Comment,
    DateTime CreatedAt);

public record DoctorReviewSummaryDto(
    Guid DoctorId,
    double AverageRating,
    int ReviewCount,
    IReadOnlyList<ReviewResponseDto> RecentReviews);

public record CenterReviewSummaryDto(
    Guid CenterId,
    double AverageRating,
    int ReviewCount,
    IReadOnlyList<ReviewResponseDto> RecentReviews);
