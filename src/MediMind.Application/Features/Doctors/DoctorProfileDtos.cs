namespace MediMind.Application.Features.Doctors;

public record DoctorProfileDto(
    Guid DoctorId,
    string Email,
    string FullName,
    string BadgeNumber,
    string Specialization,
    string LicenseNumber,
    bool LicenseVerified,
    int YearsOfExperience,
    List<string> Qualifications,
    List<string> LanguagesSpoken,
    string? ProfileImageUrl,
    string? Biography,
    List<DoctorAffiliatedCenterDto> AffiliatedCenters);

public record DoctorAffiliatedCenterDto(
    Guid CenterId,
    string CenterName,
    decimal ConsultationFee,
    DateOnly JoinedDate,
    bool IsActive);

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

public record DoctorPatientProfileDto(
    string Id,
    string FullName,
    DateOnly DateOfBirth,
    int Age,
    string Gender,
    string Phone,
    string? Email,
    string? BloodType,
    string? Address,
    DoctorEmergencyContactDto? EmergencyContact,
    List<string> ChronicConditions,
    List<string> Allergies,
    List<string> CurrentMedications,
    string? AvatarUrl,
    DateOnly? LastVisit,
    int TotalVisits);

public record DoctorEmergencyContactDto(string Name, string Phone, string? Relationship);
