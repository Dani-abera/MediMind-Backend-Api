using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.AppointmentNotes;

public interface IAppointmentNoteService
{
    Task<AppointmentNoteResponseDto?> GetAsync(Guid appointmentId, Guid doctorId, CancellationToken ct = default);
    Task<AppointmentNoteResponseDto> UpsertAsync(Guid appointmentId, Guid doctorId, UpsertAppointmentNoteDto dto, CancellationToken ct = default);
}

public class AppointmentNoteService(
    IAppointmentNoteRepository noteRepository,
    IAppointmentRepository appointmentRepository,
    IUnitOfWork unitOfWork) : IAppointmentNoteService
{
    public async Task<AppointmentNoteResponseDto?> GetAsync(Guid appointmentId, Guid doctorId, CancellationToken ct = default)
    {
        await EnsureAppointmentAccessAsync(appointmentId, doctorId, ct);
        var note = await noteRepository.GetByAppointmentAsync(appointmentId, doctorId, ct);
        return note is null ? null : MapToDto(note);
    }

    public async Task<AppointmentNoteResponseDto> UpsertAsync(Guid appointmentId, Guid doctorId, UpsertAppointmentNoteDto dto, CancellationToken ct = default)
    {
        await EnsureAppointmentAccessAsync(appointmentId, doctorId, ct);

        var existing = await noteRepository.GetByAppointmentAsync(appointmentId, doctorId, ct);
        if (existing is not null)
        {
            existing.UpdateContent(dto.Content);
            await noteRepository.UpdateAsync(existing, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return MapToDto(existing);
        }

        var note = AppointmentNote.Create(appointmentId, doctorId, dto.Content);
        var created = await noteRepository.CreateAsync(note, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapToDto(created);
    }

    private async Task EnsureAppointmentAccessAsync(Guid appointmentId, Guid doctorId, CancellationToken ct)
    {
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);

        if (appointment.DoctorId != doctorId)
            throw new ForbiddenException("You can only manage notes for your own appointments.");
    }

    private static AppointmentNoteResponseDto MapToDto(AppointmentNote n) =>
        new(n.Id, n.AppointmentId, n.DoctorId, n.Content, n.CreatedAt, n.UpdatedAt);
}
