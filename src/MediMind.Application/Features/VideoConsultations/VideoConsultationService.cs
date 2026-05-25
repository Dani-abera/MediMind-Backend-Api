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
    Task<IReadOnlyList<ConsultationSessionDto>> ListForDoctorAsync(Guid doctorId, string? status, bool today, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>WebRTC ICE server configuration entry. Pass the full array to <c>new RTCPeerConnection({ iceServers })</c>.</summary>
/// <param name="Urls">One or more STUN/TURN URLs for this entry (e.g. <c>stun:stun.l.google.com:19302</c>).</param>
/// <param name="Username">TURN username credential, <c>null</c> for STUN-only entries.</param>
/// <param name="Credential">TURN password credential, <c>null</c> for STUN-only entries.</param>
public sealed record IceServerDto(string[] Urls, string? Username, string? Credential);

/// <summary>Public profile information about a consultation participant.</summary>
public sealed record UserInfoDto(Guid UserId, string FullName, string? Specialization, string? ProfileImageUrl);

/// <summary>Represents a currently connected participant with their live SignalR connection ID.</summary>
/// <param name="ConnectionId">SignalR connection ID — use as <c>targetConnectionId</c> in WebRTC signaling hub methods.</param>
/// <param name="UserId">The participant's user ID.</param>
/// <param name="UserType"><c>Doctor</c> or <c>Patient</c>.</param>
/// <param name="DisplayName">The participant's full name for display.</param>
public sealed record ParticipantConnectionDto(string ConnectionId, Guid UserId, string UserType, string DisplayName);

/// <summary>Caller's own identity and display name as resolved from the JWT and appointment.</summary>
public sealed record ConnectionInfoDto(Guid UserId, string UserType, string DisplayName);

/// <summary>
/// Session bootstrap data returned by the Initiate endpoint. Contains ICE servers for WebRTC setup
/// and participant profile info for rendering the call screen before the peer connects.
/// </summary>
/// <param name="ConsultationId">Unique ID for this consultation session.</param>
/// <param name="AppointmentId">The appointment this consultation is linked to.</param>
/// <param name="RoomId">Opaque room identifier (used internally; SignalR hub uses <c>consultationId</c> for grouping).</param>
/// <param name="Status"><c>Scheduled</c>, <c>InProgress</c>, <c>Completed</c>, or <c>Cancelled</c>.</param>
/// <param name="JoinUrl">WebSocket base URL for the SignalR hub.</param>
/// <param name="IceServers">Pass directly to <c>RTCPeerConnection</c> constructor as <c>iceServers</c>.</param>
/// <param name="DoctorInfo">Doctor's display name, specialization, and profile image.</param>
/// <param name="PatientInfo">Patient's display name and profile image.</param>
/// <param name="AppointmentDate">Scheduled date of the appointment.</param>
/// <param name="AppointmentTime">Scheduled time of the appointment.</param>
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

/// <summary>
/// Connection bootstrapping data returned by the Join endpoint. Contains everything needed to
/// connect to the SignalR hub, initiate WebRTC signaling, and render the chat panel.
/// </summary>
/// <param name="ConsultationId">Consultation ID — pass to all hub methods.</param>
/// <param name="RoomId">Opaque room identifier.</param>
/// <param name="SignalRHubUrl">Relative hub path (<c>/hubs/video</c>). Prepend host and add <c>?access_token=</c> for WebSocket connection.</param>
/// <param name="YourConnectionInfo">Caller's own identity for rendering the local video label.</param>
/// <param name="OtherParticipants">Participants already connected at join time. Usually empty; participants arrive via <c>UserJoined</c> SignalR events in real usage.</param>
/// <param name="ChatHistory">Most recent 50 chat messages in chronological order. Render immediately on join.</param>
public sealed record ConsultationJoinDto(
    Guid ConsultationId,
    string RoomId,
    string SignalRHubUrl,
    ConnectionInfoDto YourConnectionInfo,
    IReadOnlyList<ParticipantConnectionDto> OtherParticipants,
    IReadOnlyList<ChatMessageDto> ChatHistory);

/// <summary>A single chat message exchanged during a video consultation.</summary>
/// <param name="MessageId">Unique message ID.</param>
/// <param name="ConsultationId">The consultation this message belongs to.</param>
/// <param name="SenderId">User ID of the sender.</param>
/// <param name="SenderName">Resolved full name of the sender for display.</param>
/// <param name="SenderType"><c>Doctor</c> or <c>Patient</c>.</param>
/// <param name="Content">Message text (1–2000 characters).</param>
/// <param name="SentAt">UTC timestamp when the message was persisted.</param>
/// <param name="IsRead">Whether the recipient has read this message.</param>
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
        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
            throw new DomainException("Cannot initiate a video consultation for a cancelled or no-show appointment.");

        var consultation = await videoRepository.GetByAppointmentIdAsync(appointmentId);
        if (consultation is null)
        {
            consultation = VideoConsultation.Create(appointmentId);
            await videoRepository.CreateAsync(consultation);
            await unitOfWork.SaveChangesAsync(ct);
        }

        if (consultation.Status == VideoConsultationStatus.InProgress)
            return MapSession(consultation, appointment);

        if (consultation.Status is VideoConsultationStatus.Completed or VideoConsultationStatus.Cancelled)
            throw new DomainException("This consultation has already ended.");

        consultation.Start();
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

        if (endedByDoctor && consultation.Appointment.Status is AppointmentStatus.InProgress or AppointmentStatus.Confirmed)
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

        if (consultation.Status is VideoConsultationStatus.Completed or VideoConsultationStatus.Cancelled)
            throw new DomainException("Cannot send messages in a completed or cancelled consultation.");

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

    public async Task<IReadOnlyList<ConsultationSessionDto>> ListForDoctorAsync(Guid doctorId, string? status, bool today, int page, int pageSize, CancellationToken ct = default)
    {
        VideoConsultationStatus? statusEnum = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<VideoConsultationStatus>(status, ignoreCase: true, out var parsed))
            statusEnum = parsed;

        var consultations = await videoRepository.GetByDoctorIdAsync(doctorId, statusEnum, today, page, pageSize);
        return consultations.Select(c => MapSession(c, c.Appointment)).ToList();
    }

    public async Task ReportQualityAsync(Guid consultationId, Guid userId, int bandwidth, int packetsLost, int frameRate, CancellationToken ct = default)
    {
        var consultation = await videoRepository.GetByIdAsync(consultationId)
            ?? throw new NotFoundException(nameof(VideoConsultation), consultationId);

        await videoRepository.SaveQualityMetricAsync(new VideoQualityMetric(consultationId, userId, bandwidth, packetsLost, frameRate));
        if (bandwidth < 500)
        {
            consultation.ReportQuality("Low");
            await hubNotifier.NotifyQualityAlertAsync(consultationId, userId, "Poor connection. Audio-only mode recommended", ct);
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
