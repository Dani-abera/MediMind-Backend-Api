using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.VideoConsultations;

public interface IVideoConsultationHubNotifier
{
    Task NotifyConsultationEndedAsync(Guid consultationId, string reason, CancellationToken ct = default);
    Task NotifyQualityAlertAsync(Guid consultationId, Guid userId, string message, CancellationToken ct = default);
}

public interface IVideoConsultationService
{
    Task<ConsultationSessionDto> InitiateConsultationAsync(Guid appointmentId, Guid doctorId, CancellationToken ct = default);
    Task<ConsultationJoinDto> JoinConsultationAsync(Guid consultationId, Guid userId, string userType, string? connectionId = null, CancellationToken ct = default);
    Task EndConsultationAsync(Guid consultationId, Guid endedBy, bool endedByDoctor, CancellationToken ct = default);
    Task<ChatMessageDto> SaveChatMessageAsync(Guid consultationId, Guid senderId, string senderType, string content, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessageDto>> GetChatHistoryAsync(Guid consultationId, int page, int pageSize, CancellationToken ct = default);
    Task<ConsultationSessionDto> GetByIdAsync(Guid consultationId, Guid userId, string userType, CancellationToken ct = default);
    Task<ConsultationSessionDto> GetByAppointmentAsync(Guid appointmentId, Guid userId, string userType, CancellationToken ct = default);
    Task ReportQualityAsync(Guid consultationId, Guid userId, int bandwidth, int packetsLost, int frameRate, CancellationToken ct = default);
}

public sealed record IceServerDto(string[] Urls, string? Username, string? Credential);
public sealed record UserInfoDto(Guid UserId, string FullName, string? Specialization, string? ProfileImageUrl);
public sealed record ParticipantConnectionDto(string ConnectionId, Guid UserId, string UserType, string DisplayName);
public sealed record ConnectionInfoDto(Guid UserId, string UserType, string DisplayName);

public sealed record ConsultationSessionDto(
    Guid ConsultationId,
    Guid AppointmentId,
    string RoomId,
    string Status,
    string JoinUrl,
    IReadOnlyList<IceServerDto> IceServers,
    UserInfoDto DoctorInfo,
    UserInfoDto PatientInfo,
    DateOnly AppointmentDate,
    TimeOnly AppointmentTime);

public sealed record ConsultationJoinDto(
    Guid ConsultationId,
    string RoomId,
    string SignalRHubUrl,
    ConnectionInfoDto YourConnectionInfo,
    IReadOnlyList<ParticipantConnectionDto> OtherParticipants,
    IReadOnlyList<ChatMessageDto> ChatHistory);

public sealed record ChatMessageDto(
    Guid MessageId,
    Guid ConsultationId,
    Guid SenderId,
    string SenderName,
    string SenderType,
    string Content,
    DateTime SentAt,
    bool IsRead);

public sealed class VideoConsultationService(
    IVideoConsultationRepository videoRepository,
    IAppointmentRepository appointmentRepository,
    IPushNotificationService pushNotificationService,
    IVideoConsultationHubNotifier hubNotifier,
    IUnitOfWork unitOfWork) : IVideoConsultationService
{
    private static readonly IceServerDto[] DefaultIceServers =
    [
        new(["stun:stun.l.google.com:19302"], null, null)
    ];

    public async Task<ConsultationSessionDto> InitiateConsultationAsync(Guid appointmentId, Guid doctorId, CancellationToken ct = default)
    {
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);
        if (appointment.DoctorId != doctorId)
            throw new ForbiddenException("Only the assigned doctor can initiate this consultation.");
        if (appointment.Status != AppointmentStatus.Confirmed)
            throw new DomainException("Appointment must be confirmed before starting a video consultation.");

        var existing = await videoRepository.GetByAppointmentIdAsync(appointmentId);
        if (existing is not null && existing.Status is VideoConsultationStatus.Scheduled or VideoConsultationStatus.InProgress)
            throw new DomainException("An active consultation already exists for this appointment.");

        var consultation = VideoConsultation.Create(appointmentId);
        await videoRepository.CreateAsync(consultation);
        await unitOfWork.SaveChangesAsync(ct);

        await pushNotificationService.SendToUserAsync(
            appointment.PatientId,
            $"Dr. {appointment.Doctor.FullName} is ready",
            "Dr. has started your video consultation. Tap to join.",
            new Dictionary<string, string>
            {
                ["consultationId"] = consultation.Id.ToString(),
                ["roomId"] = consultation.RoomId
            },
            ct);

        return MapSession(consultation, appointment);
    }

    public async Task<ConsultationJoinDto> JoinConsultationAsync(Guid consultationId, Guid userId, string userType, string? connectionId = null, CancellationToken ct = default)
    {
        var consultation = await videoRepository.GetByIdAsync(consultationId)
            ?? throw new NotFoundException(nameof(VideoConsultation), consultationId);
        var appointment = consultation.Appointment;
        if (consultation.Status is VideoConsultationStatus.Completed or VideoConsultationStatus.Cancelled)
            throw new DomainException("This consultation is no longer active.");

        var isDoctor = string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase);
        var isPatient = string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase);
        if ((isDoctor && appointment.DoctorId != userId) || (isPatient && appointment.PatientId != userId) || (!isDoctor && !isPatient))
            throw new ForbiddenException("You are not allowed to join this consultation.");

        if (consultation.Status == VideoConsultationStatus.Scheduled)
            consultation.Start();

        var participant = new VideoConsultationParticipant(
            consultation.Id,
            isPatient ? userId : null,
            isDoctor ? userId : null);
        await videoRepository.AddParticipantAsync(participant);
        await unitOfWork.SaveChangesAsync(ct);

        var history = await GetChatHistoryAsync(consultation.Id, 1, 50, ct);
        return new ConsultationJoinDto(
            consultation.Id,
            consultation.RoomId,
            "/hubs/video",
            new ConnectionInfoDto(userId, userType, isDoctor ? appointment.Doctor.FullName : appointment.Patient.FullName),
            [],
            history);
    }

    public async Task EndConsultationAsync(Guid consultationId, Guid endedBy, bool endedByDoctor, CancellationToken ct = default)
    {
        var consultation = await videoRepository.GetByIdAsync(consultationId)
            ?? throw new NotFoundException(nameof(VideoConsultation), consultationId);
        if (consultation.Status is VideoConsultationStatus.Completed or VideoConsultationStatus.Cancelled)
            return;

        consultation.End();
        await videoRepository.UpdateParticipantLeftAsync(consultationId, consultation.Appointment.PatientId);
        await videoRepository.UpdateParticipantLeftAsync(consultationId, consultation.Appointment.DoctorId);

        if (endedByDoctor && consultation.Appointment.Status == AppointmentStatus.InProgress)
            consultation.Appointment.MarkCompleted();

        await unitOfWork.SaveChangesAsync(ct);
        await hubNotifier.NotifyConsultationEndedAsync(consultationId, "Consultation has ended", ct);
    }

    public async Task<ChatMessageDto> SaveChatMessageAsync(Guid consultationId, Guid senderId, string senderType, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 2000)
            throw new DomainException("Message content must be between 1 and 2000 characters.");

        var consultation = await videoRepository.GetByIdAsync(consultationId)
            ?? throw new NotFoundException(nameof(VideoConsultation), consultationId);
        var senderName = string.Equals(senderType, "Doctor", StringComparison.OrdinalIgnoreCase)
            ? consultation.Appointment.Doctor.FullName
            : consultation.Appointment.Patient.FullName;

        var message = new ChatMessage(consultationId, senderId, senderType, content);
        await videoRepository.SaveMessageAsync(message);
        await unitOfWork.SaveChangesAsync(ct);

        return new ChatMessageDto(
            message.Id,
            consultationId,
            senderId,
            senderName,
            senderType,
            message.Content,
            message.SentAt,
            message.IsRead);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetChatHistoryAsync(Guid consultationId, int page, int pageSize, CancellationToken ct = default)
    {
        var consultation = await videoRepository.GetByIdAsync(consultationId)
            ?? throw new NotFoundException(nameof(VideoConsultation), consultationId);
        var messages = await videoRepository.GetChatHistoryAsync(consultationId, page, pageSize);

        return messages
            .OrderBy(m => m.SentAt)
            .Select(m => new ChatMessageDto(
                m.Id,
                m.ConsultationId,
                m.SenderId,
                m.SenderId == consultation.Appointment.DoctorId ? consultation.Appointment.Doctor.FullName : consultation.Appointment.Patient.FullName,
                m.SenderType,
                m.Content,
                m.SentAt,
                m.IsRead))
            .ToList();
    }

    public async Task<ConsultationSessionDto> GetByIdAsync(Guid consultationId, Guid userId, string userType, CancellationToken ct = default)
    {
        var consultation = await videoRepository.GetByIdAsync(consultationId)
            ?? throw new NotFoundException(nameof(VideoConsultation), consultationId);
        EnsureAccess(consultation, userId, userType);
        return MapSession(consultation, consultation.Appointment);
    }

    public async Task<ConsultationSessionDto> GetByAppointmentAsync(Guid appointmentId, Guid userId, string userType, CancellationToken ct = default)
    {
        var consultation = await videoRepository.GetByAppointmentIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(VideoConsultation), appointmentId);
        EnsureAccess(consultation, userId, userType);
        return MapSession(consultation, consultation.Appointment);
    }

    public async Task ReportQualityAsync(Guid consultationId, Guid userId, int bandwidth, int packetsLost, int frameRate, CancellationToken ct = default)
    {
        var consultation = await videoRepository.GetByIdAsync(consultationId)
            ?? throw new NotFoundException(nameof(VideoConsultation), consultationId);

        await videoRepository.SaveQualityMetricAsync(new VideoQualityMetric(consultationId, userId, bandwidth, packetsLost, frameRate));
        if (bandwidth < 500)
        {
            consultation.ReportQuality("Low");
            await hubNotifier.NotifyQualityAlertAsync(consultationId, userId, "Poor connection detected. Consider switching to audio-only mode.", ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private static ConsultationSessionDto MapSession(VideoConsultation consultation, Appointment appointment) =>
        new(
            consultation.Id,
            consultation.AppointmentId,
            consultation.RoomId,
            consultation.Status.ToString(),
            "wss://api.medimind.et/hubs/video",
            DefaultIceServers,
            new UserInfoDto(appointment.DoctorId, appointment.Doctor.FullName, appointment.Doctor.Specialization, appointment.Doctor.ProfileImageUrl),
            new UserInfoDto(appointment.PatientId, appointment.Patient.FullName, null, appointment.Patient.ProfileImageUrl),
            appointment.AppointmentDate,
            appointment.AppointmentTime);

    private static void EnsureAccess(VideoConsultation consultation, Guid userId, string userType)
    {
        if (string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase) && consultation.Appointment.DoctorId == userId)
            return;
        if (string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase) && consultation.Appointment.PatientId == userId)
            return;
        throw new ForbiddenException("Access denied to this consultation.");
    }
}
