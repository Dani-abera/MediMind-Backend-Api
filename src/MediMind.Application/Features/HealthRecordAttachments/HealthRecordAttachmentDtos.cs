namespace MediMind.Application.Features.HealthRecordAttachments;

public record HealthRecordAttachmentResponseDto(
    Guid AttachmentId,
    Guid HealthRecordId,
    string FileName,
    string FileType,
    long FileSizeBytes,
    string StoragePath,
    DateTime UploadedAt);
