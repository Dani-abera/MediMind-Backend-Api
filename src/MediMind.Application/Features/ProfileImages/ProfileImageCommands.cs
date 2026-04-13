using FluentValidation;
using MediatR;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.ProfileImages;

public record ProfileImageUrlDto(string? ProfileImageUrl);

// ─── Current user (patient, doctor, admin, super-admin) ─────────────────────────

public record GetMyProfileImageQuery : IRequest<ProfileImageUrlDto>;

public record UploadMyProfileImageCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileLength) : IRequest<ProfileImageUrlDto>;

public record DeleteMyProfileImageCommand : IRequest<Unit>;

// ─── Healthcare center branding ───────────────────────────────────────────────

public record GetHealthcareCenterProfileImageQuery(Guid CenterId) : IRequest<ProfileImageUrlDto>;

public record UploadHealthcareCenterProfileImageCommand(
    Guid CenterId,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileLength) : IRequest<ProfileImageUrlDto>;

public record DeleteHealthcareCenterProfileImageCommand(Guid CenterId) : IRequest<Unit>;

public static class ProfileImageRules
{
    public const string Folder = "profile-images";
    public const long MaxBytes = 5 * 1024 * 1024;

    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    public static bool IsAllowed(string contentType) =>
        AllowedContentTypes.Contains(contentType.Trim());
}

public class UploadMyProfileImageValidator : AbstractValidator<UploadMyProfileImageCommand>
{
    public UploadMyProfileImageValidator()
    {
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.ContentType).NotEmpty()
            .Must(ProfileImageRules.IsAllowed)
            .WithMessage("Only JPEG, PNG, WebP, and GIF images are allowed.");
        RuleFor(x => x.FileLength).InclusiveBetween(1, ProfileImageRules.MaxBytes)
            .WithMessage($"Image must be at most {ProfileImageRules.MaxBytes / 1024 / 1024} MB.");
    }
}

public class UploadHealthcareCenterProfileImageValidator : AbstractValidator<UploadHealthcareCenterProfileImageCommand>
{
    public UploadHealthcareCenterProfileImageValidator()
    {
        RuleFor(x => x.CenterId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.ContentType).NotEmpty()
            .Must(ProfileImageRules.IsAllowed)
            .WithMessage("Only JPEG, PNG, WebP, and GIF images are allowed.");
        RuleFor(x => x.FileLength).InclusiveBetween(1, ProfileImageRules.MaxBytes)
            .WithMessage($"Image must be at most {ProfileImageRules.MaxBytes / 1024 / 1024} MB.");
    }
}

public class GetMyProfileImageHandler(IUserRepository userRepository, ICurrentUser currentUser)
    : IRequestHandler<GetMyProfileImageQuery, ProfileImageUrlDto>
{
    public async Task<ProfileImageUrlDto> Handle(GetMyProfileImageQuery _, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(currentUser.UserId, ct)
                   ?? throw new NotFoundException(nameof(User), currentUser.UserId);

        return new ProfileImageUrlDto(user.ProfileImageUrl);
    }
}

public class UploadMyProfileImageHandler(
    IUserRepository userRepository,
    ICurrentUser currentUser,
    IStorageService storageService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UploadMyProfileImageCommand, ProfileImageUrlDto>
{
    public async Task<ProfileImageUrlDto> Handle(UploadMyProfileImageCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(currentUser.UserId, ct)
                   ?? throw new NotFoundException(nameof(User), currentUser.UserId);

        if (!string.IsNullOrEmpty(user.ProfileImageUrl))
            await storageService.DeleteAsync(user.ProfileImageUrl, ct);

        var safeFileName = $"{user.Id:N}_{Path.GetFileName(request.FileName.Replace('\\', '/'))}";
        var url = await storageService.UploadAsync(
            request.FileStream,
            safeFileName,
            ProfileImageRules.Folder,
            ct);

        user.SetProfileImageUrl(url);
        await unitOfWork.SaveChangesAsync(ct);

        return new ProfileImageUrlDto(url);
    }
}

public class DeleteMyProfileImageHandler(
    IUserRepository userRepository,
    ICurrentUser currentUser,
    IStorageService storageService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteMyProfileImageCommand, Unit>
{
    public async Task<Unit> Handle(DeleteMyProfileImageCommand _, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(currentUser.UserId, ct)
                   ?? throw new NotFoundException(nameof(User), currentUser.UserId);

        if (!string.IsNullOrEmpty(user.ProfileImageUrl))
        {
            await storageService.DeleteAsync(user.ProfileImageUrl, ct);
            user.SetProfileImageUrl(null);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return Unit.Value;
    }
}

public class GetHealthcareCenterProfileImageHandler(IHealthcareCenterRepository centerRepository)
    : IRequestHandler<GetHealthcareCenterProfileImageQuery, ProfileImageUrlDto>
{
    public async Task<ProfileImageUrlDto> Handle(GetHealthcareCenterProfileImageQuery request, CancellationToken ct)
    {
        var center = await centerRepository.GetByIdAsync(request.CenterId, ct)
                     ?? throw new NotFoundException(nameof(HealthcareCenter), request.CenterId);

        return new ProfileImageUrlDto(center.ProfileImageUrl);
    }
}

public class UploadHealthcareCenterProfileImageHandler(
    IHealthcareCenterRepository centerRepository,
    ICurrentUser currentUser,
    IStorageService storageService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UploadHealthcareCenterProfileImageCommand, ProfileImageUrlDto>
{
    public async Task<ProfileImageUrlDto> Handle(UploadHealthcareCenterProfileImageCommand request, CancellationToken ct)
    {
        if (!CenterProfilePolicy.CanManage(currentUser, request.CenterId))
            throw new TenantIsolationException();

        var center = await centerRepository.GetByIdAsync(request.CenterId, ct)
                     ?? throw new NotFoundException(nameof(HealthcareCenter), request.CenterId);

        if (!string.IsNullOrEmpty(center.ProfileImageUrl))
            await storageService.DeleteAsync(center.ProfileImageUrl, ct);

        var safeFileName = $"{request.CenterId:N}_{Path.GetFileName(request.FileName.Replace('\\', '/'))}";
        var url = await storageService.UploadAsync(
            request.FileStream,
            safeFileName,
            ProfileImageRules.Folder,
            ct);

        center.SetProfileImageUrl(url);
        await unitOfWork.SaveChangesAsync(ct);

        return new ProfileImageUrlDto(url);
    }
}

public class DeleteHealthcareCenterProfileImageHandler(
    IHealthcareCenterRepository centerRepository,
    ICurrentUser currentUser,
    IStorageService storageService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteHealthcareCenterProfileImageCommand, Unit>
{
    public async Task<Unit> Handle(DeleteHealthcareCenterProfileImageCommand request, CancellationToken ct)
    {
        if (!CenterProfilePolicy.CanManage(currentUser, request.CenterId))
            throw new TenantIsolationException();

        var center = await centerRepository.GetByIdAsync(request.CenterId, ct)
                     ?? throw new NotFoundException(nameof(HealthcareCenter), request.CenterId);

        if (!string.IsNullOrEmpty(center.ProfileImageUrl))
        {
            await storageService.DeleteAsync(center.ProfileImageUrl, ct);
            center.SetProfileImageUrl(null);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return Unit.Value;
    }
}

file static class CenterProfilePolicy
{
    public static bool CanManage(ICurrentUser user, Guid centerId)
    {
        if (string.Equals(user.UserType, nameof(UserType.SuperAdmin), StringComparison.Ordinal))
            return true;
        if (string.Equals(user.UserType, nameof(UserType.Admin), StringComparison.Ordinal))
            return user.TenantId == centerId;
        return false;
    }
}
