namespace MediMind.Application.Features.AppointmentNotes;

public record UpsertAppointmentNoteDto(string Content);

public record AppointmentNoteResponseDto(
    Guid NoteId,
    Guid AppointmentId,
    Guid DoctorId,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);
