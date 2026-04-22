using AutoMapper;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.HealthRecordAttachments;

public interface IHealthRecordAttachmentService
{
    Task<HealthRecordAttachmentResponseDto> UploadAsync(
        Guid healthRecordId,
        Guid patientId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        CancellationToken ct = default);

    Task<IReadOnlyList<HealthRecordAttachmentResponseDto>> GetByRecordAsync(
        Guid healthRecordId,
        Guid patientId,
        CancellationToken ct = default);

    Task<HealthRecordAttachmentResponseDto?> GetByIdAsync(
        Guid healthRecordId,
        Guid attachmentId,
        Guid patientId,
        CancellationToken ct = default);

    Task DeleteAsync(
        Guid healthRecordId,
        Guid attachmentId,
        Guid patientId,
        CancellationToken ct = default);
}

public class HealthRecordAttachmentService(
    IHealthRecordAttachmentRepository attachmentRepository,
    IHealthRecordRepository healthRecordRepository,
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IHealthRecordAttachmentService
{
    private static readonly string[] AllowedMimeTypes = ["application/pdf", "image/jpeg", "image/png"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public async Task<HealthRecordAttachmentResponseDto> UploadAsync(
        Guid healthRecordId, Guid patientId, Stream fileStream,
        string fileName, string contentType, long fileSizeBytes, CancellationToken ct = default)
    {
        ValidateFile(fileName, contentType, fileSizeBytes);

        var record = await healthRecordRepository.GetByIdAsync(healthRecordId, patientId)
            ?? throw new NotFoundException(nameof(HealthRecord), healthRecordId);

        var safeFileName = Path.GetFileName(fileName.Replace('\\', '/'));
        var storagePath = await storageService.UploadAsync(fileStream, safeFileName, "health-attachments", ct);

        var attachment = HealthRecordAttachment.Create(healthRecordId, safeFileName, contentType, fileSizeBytes, storagePath);
        await attachmentRepository.AddAsync(attachment, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return mapper.Map<HealthRecordAttachmentResponseDto>(attachment);
    }

    public async Task<IReadOnlyList<HealthRecordAttachmentResponseDto>> GetByRecordAsync(
        Guid healthRecordId, Guid patientId, CancellationToken ct = default)
    {
        var record = await healthRecordRepository.GetByIdAsync(healthRecordId, patientId)
            ?? throw new NotFoundException(nameof(HealthRecord), healthRecordId);

        var attachments = await attachmentRepository.GetByRecordIdAsync(healthRecordId, ct);
        return mapper.Map<IReadOnlyList<HealthRecordAttachmentResponseDto>>(attachments);
    }

    public async Task<HealthRecordAttachmentResponseDto?> GetByIdAsync(
        Guid healthRecordId, Guid attachmentId, Guid patientId, CancellationToken ct = default)
    {
        _ = await healthRecordRepository.GetByIdAsync(healthRecordId, patientId)
            ?? throw new NotFoundException(nameof(HealthRecord), healthRecordId);

        var attachment = await attachmentRepository.GetByIdAsync(attachmentId, healthRecordId, ct);
        return attachment is null ? null : mapper.Map<HealthRecordAttachmentResponseDto>(attachment);
    }

    public async Task DeleteAsync(
        Guid healthRecordId, Guid attachmentId, Guid patientId, CancellationToken ct = default)
    {
        _ = await healthRecordRepository.GetByIdAsync(healthRecordId, patientId)
            ?? throw new NotFoundException(nameof(HealthRecord), healthRecordId);

        var attachment = await attachmentRepository.GetByIdAsync(attachmentId, healthRecordId, ct)
            ?? throw new NotFoundException(nameof(HealthRecordAttachment), attachmentId);

        await storageService.DeleteAsync(attachment.StoragePath, ct);
        await attachmentRepository.DeleteAsync(attachment, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static void ValidateFile(string fileName, string contentType, long fileSizeBytes)
    {
        if (!AllowedMimeTypes.Contains(contentType))
            throw new DomainException($"File type '{contentType}' is not allowed. Allowed: {string.Join(", ", AllowedMimeTypes)}.");

        if (fileSizeBytes > MaxFileSizeBytes)
            throw new DomainException("File size exceeds the 10 MB limit.");

        var safeName = Path.GetFileName(fileName);
        if (safeName != fileName.Replace('\\', '/').Split('/').Last())
            throw new DomainException("Invalid file name.");
    }
}
