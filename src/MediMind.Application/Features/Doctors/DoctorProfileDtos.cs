namespace MediMind.Application.Features.Doctors;

public record DoctorProfileDto(
    Guid DoctorId,
    string FullName,
    string Specialization,
    string LicenseNumber,
    int YearsOfExperience,
    string? Qualifications,
    List<string> LanguagesSpoken,
    string? ProfileImageUrl,
    string? Biography,
    List<DoctorAffiliatedCenterDto> AffiliatedCenters);

public record DoctorAffiliatedCenterDto(
    Guid CenterId,
    string CenterName,
    decimal ConsultationFee,
    DateOnly JoinedDate);

public record UpdateDoctorProfileDto(
    string? Biography,
    List<string>? LanguagesSpoken,
    string? Qualifications,
    string? ProfileImageUrl);

public record DoctorTodayAppointmentDto(
    Guid AppointmentId,
    string Status,
    DateOnly AppointmentDate,
    TimeOnly AppointmentTime,
    string ReasonForVisit,
    string? QueueNumber,
    int? EstimatedWaitMinutes,
    DoctorPatientSummaryDto Patient,
    int PrescriptionCount);

public record DoctorPatientSummaryDto(
    Guid PatientId,
    string FullName,
    string PhoneNumber,
    DateOnly? LastVisit,
    int TotalVisits);

public record DoctorQueueItemDto(
    string QueueNumber,
    int Position,
    int EstimatedWaitMinutes,
    string Status,
    Guid PatientId,
    string PatientName,
    TimeOnly AppointmentTime);

public record DoctorPatientsPageDto(
    IReadOnlyList<DoctorPatientSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
