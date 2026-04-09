using MediatR;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.VideoConsultations;

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record VideoConsultationDto(
    Guid Id,
    Guid AppointmentId,
    string RoomId,
    string Status,
    DateTime? StartTime,
    DateTime? EndTime,
    int? DurationMinutes);

// ═══════════════════════════════════════════════════════════════════════════════
// START VIDEO CONSULTATION (Doctor initiates — FR-017)
// ═══════════════════════════════════════════════════════════════════════════════

public record StartVideoConsultationCommand(
    Guid AppointmentId,
    Guid DoctorId
) : IRequest<VideoConsultationDto>;

public class StartVideoConsultationHandler(
    IAppointmentRepository appointmentRepository,
    IRepository<VideoConsultation> consultationRepository,
    IPushNotificationService pushService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<StartVideoConsultationCommand, VideoConsultationDto>
{
    public async Task<VideoConsultationDto> Handle(
        StartVideoConsultationCommand request, CancellationToken ct)
    {
        var appointment = await appointmentRepository.GetByIdAsync(request.AppointmentId, ct)
                          ?? throw new NotFoundException(nameof(Appointment), request.AppointmentId);

        if (appointment.DoctorId != request.DoctorId)
            throw new ForbiddenException("You can only initiate consultations for your own appointments.");

        if (appointment.Status is not AppointmentStatus.Confirmed and not AppointmentStatus.InProgress)
            throw new DomainException("Appointment must be confirmed or in-progress to start video call.");

        // Create or retrieve existing video consultation
        var consultation = appointment.VideoConsultation;
        if (consultation is null)
        {
            consultation = VideoConsultation.Create(request.AppointmentId);
            await consultationRepository.AddAsync(consultation, ct);
        }

        consultation.Start();
        appointment.MarkInProgress();
        await unitOfWork.SaveChangesAsync(ct);

        // Notify patient that doctor is ready
        await pushService.SendToUserAsync(
            appointment.PatientId,
            "Doctor is Ready 🎥",
            "Your video consultation is starting. Click to join.",
            new Dictionary<string, string>
            {
                ["roomId"] = consultation.RoomId,
                ["consultationId"] = consultation.Id.ToString(),
                ["appointmentId"] = request.AppointmentId.ToString()
            },
            ct);

        return new VideoConsultationDto(
            consultation.Id,
            consultation.AppointmentId,
            consultation.RoomId,
            consultation.Status.ToString(),
            consultation.StartTime,
            consultation.EndTime,
            consultation.DurationMinutes);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// JOIN VIDEO CONSULTATION (Patient — FR-017)
// ═══════════════════════════════════════════════════════════════════════════════

public record JoinVideoConsultationCommand(
    Guid AppointmentId,
    Guid PatientId
) : IRequest<VideoConsultationDto>;

public class JoinVideoConsultationHandler(
    IAppointmentRepository appointmentRepository,
    IRepository<VideoConsultationParticipant> participantRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<JoinVideoConsultationCommand, VideoConsultationDto>
{
    public async Task<VideoConsultationDto> Handle(
        JoinVideoConsultationCommand request, CancellationToken ct)
    {
        var appointment = await appointmentRepository.GetByIdAsync(request.AppointmentId, ct)
                          ?? throw new NotFoundException(nameof(Appointment), request.AppointmentId);

        if (appointment.PatientId != request.PatientId)
            throw new ForbiddenException("You can only join your own consultations.");

        var consultation = appointment.VideoConsultation
                           ?? throw new DomainException("Video consultation has not been started yet. Please wait for your doctor.");

        if (consultation.Status != ConsultationStatus.InProgress)
            throw new DomainException("Video consultation is not currently active.");

        // Record participant join
        var participant = new VideoConsultationParticipant(
            consultation.Id,
            patientId: request.PatientId,
            doctorId: null);
        await participantRepository.AddAsync(participant, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new VideoConsultationDto(
            consultation.Id,
            consultation.AppointmentId,
            consultation.RoomId,
            consultation.Status.ToString(),
            consultation.StartTime,
            consultation.EndTime,
            consultation.DurationMinutes);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// END VIDEO CONSULTATION (Doctor — FR-017)
// ═══════════════════════════════════════════════════════════════════════════════

public record EndVideoConsultationCommand(
    Guid AppointmentId,
    Guid DoctorId,
    string? VideoQuality
) : IRequest<VideoConsultationDto>;

public class EndVideoConsultationHandler(
    IAppointmentRepository appointmentRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<EndVideoConsultationCommand, VideoConsultationDto>
{
    public async Task<VideoConsultationDto> Handle(
        EndVideoConsultationCommand request, CancellationToken ct)
    {
        var appointment = await appointmentRepository.GetByIdAsync(request.AppointmentId, ct)
                          ?? throw new NotFoundException(nameof(Appointment), request.AppointmentId);

        if (appointment.DoctorId != request.DoctorId)
            throw new ForbiddenException("You can only end your own consultations.");

        var consultation = appointment.VideoConsultation
                           ?? throw new DomainException("No active video consultation found.");

        consultation.End(request.VideoQuality);
        appointment.MarkCompleted();
        await unitOfWork.SaveChangesAsync(ct);

        return new VideoConsultationDto(
            consultation.Id,
            consultation.AppointmentId,
            consultation.RoomId,
            consultation.Status.ToString(),
            consultation.StartTime,
            consultation.EndTime,
            consultation.DurationMinutes);
    }
}
