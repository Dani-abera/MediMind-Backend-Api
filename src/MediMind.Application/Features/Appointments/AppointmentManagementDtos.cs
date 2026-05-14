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
    Guid Id,
    string Status,
    DateTime DateTime,
    int DurationMinutes,
    string Reason,
    string? Symptoms,
    DateTime BookingDate,
    // Patient — flat
    Guid PatientId,
    string PatientName,
    string? PatientPhone,
    // Doctor — flat
    Guid DoctorId,
    string DoctorName,
    string? DoctorSpecialization,
    string? DoctorAvatarUrl,
    // Center — flat
    Guid CenterId,
    string CenterName,
    string? CenterAddress,
    string? CenterPhone,
    double? CenterLatitude,
    double? CenterLongitude,
    // Appointment
    string Type,
    bool CanCancel,
    bool CanReschedule,
    int? QueueNumber,
    int? EstimatedWaitMinutes,
    bool RequiresPayment = false,
    string? PaymentInitiationUrl = null,
    Guid? PaymentId = null,
    string? PaymentStatus = null,
    bool CanInitiateVideoConsultation = false,
    Guid? VideoConsultationId = null,
    int CancellationPolicyHours = 2);

public record AvailabilityResponseDto(
    Guid DoctorId,
    Guid CenterId,
    DateOnly Date,
    List<AvailabilitySlotDto> Slots,
    DateOnly? NextAvailableDate);

public record AvailabilitySlotDto(string Time, bool IsAvailable);

public record TimeSlot(TimeOnly Time, bool IsAvailable, int SlotDuration);

public record RescheduleCountDto(Guid AppointmentId, int RescheduleCount, bool CanReschedule);

public record CalendarDayDto(DateOnly Date, List<AppointmentResponseDto> Appointments);

public record BulkApproveDto(List<Guid> Ids);

public record CalendarSyncResponseDto<T>(
    T Data,
    DateTime LastSyncedAt,
    bool IsOnline = true,
    string? ErrorMessage = null);

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
