using MediMind.Domain.Enums;

namespace MediMind.Application.Features.Appointments;

public record CreateAppointmentDto(
    Guid CenterId,
    Guid DoctorId,
    DateOnly AppointmentDate,
    TimeOnly AppointmentTime,
    string ReasonForVisit,
    string? Symptoms);

public record CancelAppointmentDto(string CancellationReason);

public record RescheduleAppointmentDto(DateOnly NewDate, TimeOnly NewTime, string? Reason);

public record ApproveRejectDto(string? Reason);

public record AppointmentResponseDto(
    Guid AppointmentId,
    string Status,
    DateOnly AppointmentDate,
    TimeOnly AppointmentTime,
    int DurationMinutes,
    string ReasonForVisit,
    DateTime BookingDate,
    AppointmentPatientDto Patient,
    AppointmentDoctorDto Doctor,
    AppointmentCenterDto Center,
    bool CanCancel,
    bool CanReschedule,
    string? QueueNumber,
    int? EstimatedWaitTime,
    bool RequiresPayment = false,
    string? PaymentInitiationUrl = null);

public record AppointmentPatientDto(Guid PatientId, string FullName, string PhoneNumber);
public record AppointmentDoctorDto(Guid DoctorId, string FullName, string Specialization);
public record AppointmentCenterDto(Guid CenterId, string CenterName, string Address, string PhoneNumber);

public record AvailabilityResponseDto(
    Guid DoctorId,
    Guid CenterId,
    DateOnly Date,
    List<AvailabilitySlotDto> Slots,
    DateOnly? NextAvailableDate);

public record AvailabilitySlotDto(string Time, bool IsAvailable);

public record TimeSlot(TimeOnly Time, bool IsAvailable, int SlotDuration);

public record RescheduleCountDto(Guid AppointmentId, int RescheduleCount, bool CanReschedule);

public record WaitlistSubscribeDto(
    Guid DoctorId,
    Guid CenterId,
    DateOnly PreferredDateFrom,
    DateOnly PreferredDateTo);

public record WaitlistResponseDto(
    Guid SubscriptionId,
    Guid PatientId,
    Guid DoctorId,
    Guid CenterId,
    DateOnly PreferredDateFrom,
    DateOnly PreferredDateTo,
    bool IsActive,
    DateTime? NotifiedAt);
