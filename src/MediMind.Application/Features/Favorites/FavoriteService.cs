using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.Favorites;

public interface IFavoriteService
{
    Task<IReadOnlyList<FavoriteDoctorResponseDto>> GetDoctorFavoritesAsync(Guid patientId, CancellationToken ct = default);
    Task AddDoctorFavoriteAsync(Guid patientId, Guid doctorId, CancellationToken ct = default);
    Task RemoveDoctorFavoriteAsync(Guid patientId, Guid doctorId, CancellationToken ct = default);

    Task<IReadOnlyList<FavoriteCenterResponseDto>> GetCenterFavoritesAsync(Guid patientId, CancellationToken ct = default);
    Task AddCenterFavoriteAsync(Guid patientId, Guid centerId, CancellationToken ct = default);
    Task RemoveCenterFavoriteAsync(Guid patientId, Guid centerId, CancellationToken ct = default);
}

public class FavoriteService(
    IFavoriteRepository favoriteRepository,
    IDoctorRepository doctorRepository,
    IHealthcareCenterRepository centerRepository,
    IUnitOfWork unitOfWork) : IFavoriteService
{
    public async Task<IReadOnlyList<FavoriteDoctorResponseDto>> GetDoctorFavoritesAsync(Guid patientId, CancellationToken ct = default)
    {
        var favorites = await favoriteRepository.GetDoctorFavoritesByPatientAsync(patientId, ct);
        var result = new List<FavoriteDoctorResponseDto>();

        foreach (var fav in favorites)
        {
            var doctor = await doctorRepository.GetByIdAsync(fav.DoctorId!.Value);
            if (doctor is not null)
                result.Add(new FavoriteDoctorResponseDto(
                    fav.Id, doctor.Id, doctor.FullName, doctor.Specialization,
                    doctor.ProfileImageUrl, fav.CreatedAt));
        }

        return result;
    }

    public async Task AddDoctorFavoriteAsync(Guid patientId, Guid doctorId, CancellationToken ct = default)
    {
        _ = await doctorRepository.GetByIdAsync(doctorId)
            ?? throw new NotFoundException(nameof(Doctor), doctorId);

        if (await favoriteRepository.IsDoctorFavoriteAsync(patientId, doctorId, ct))
            return; // idempotent

        var fav = Favorite.ForDoctor(patientId, doctorId);
        await favoriteRepository.AddAsync(fav, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RemoveDoctorFavoriteAsync(Guid patientId, Guid doctorId, CancellationToken ct = default)
    {
        var fav = await favoriteRepository.GetDoctorFavoriteAsync(patientId, doctorId, ct);
        if (fav is null) return;
        await favoriteRepository.DeleteAsync(fav, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FavoriteCenterResponseDto>> GetCenterFavoritesAsync(Guid patientId, CancellationToken ct = default)
    {
        var favorites = await favoriteRepository.GetCenterFavoritesByPatientAsync(patientId, ct);
        var result = new List<FavoriteCenterResponseDto>();

        foreach (var fav in favorites)
        {
            var center = await centerRepository.GetByIdAsync(fav.CenterId!.Value);
            if (center is not null)
                result.Add(new FavoriteCenterResponseDto(
                    fav.Id, center.Id, center.CenterName, center.City,
                    center.ProfileImageUrl, fav.CreatedAt));
        }

        return result;
    }

    public async Task AddCenterFavoriteAsync(Guid patientId, Guid centerId, CancellationToken ct = default)
    {
        _ = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        if (await favoriteRepository.IsCenterFavoriteAsync(patientId, centerId, ct))
            return;

        var fav = Favorite.ForCenter(patientId, centerId);
        await favoriteRepository.AddAsync(fav, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RemoveCenterFavoriteAsync(Guid patientId, Guid centerId, CancellationToken ct = default)
    {
        var fav = await favoriteRepository.GetCenterFavoriteAsync(patientId, centerId, ct);
        if (fav is null) return;
        await favoriteRepository.DeleteAsync(fav, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
