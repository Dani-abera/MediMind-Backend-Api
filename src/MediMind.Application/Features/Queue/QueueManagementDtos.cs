namespace MediMind.Application.Features.Queue;

public record QueueItemDto(
    Guid QueueId,
    Guid AppointmentId,
    string QueueNumber,
    int Position,
    string Status,
    int EstimatedWaitTimeMinutes,
    string PatientName,
    string DoctorName,
    TimeOnly AppointmentTime,
    DateTime? CalledTime,
    DateTime? ConsultationStartTime,
    string? RoomNumber);

public record PatientQueueStatusDto(
    Guid QueueId,
    string QueueNumber,
    int CurrentPosition,
    int EstimatedWaitTimeMinutes,
    string FormattedWaitTime,
    string Status,
    string StatusMessage,
    QueueCenterInfoDto Center,
    QueueDoctorInfoDto Doctor,
    TimeOnly AppointmentTime);

public record QueueCenterInfoDto(string CenterName, string Address);
public record QueueDoctorInfoDto(string DoctorName, string Specialization);

public record AdminQueueDashboardDto(
    Guid CenterId,
    string CenterName,
    DateOnly Date,
    int TotalInQueue,
    int WaitingCount,
    int InConsultationCount,
    int CompletedCount,
    int MissedCount,
    double? AverageConsultationMinutes,
    QueueItemDto? CurrentConsultation,
    List<QueueItemDto> WaitingPatients);

public record CenterQueueDto(
    List<QueueItemDto> Items,
    int TotalInQueue,
    int WaitingCount,
    int InConsultationCount,
    int CompletedCount,
    int MissedCount);
