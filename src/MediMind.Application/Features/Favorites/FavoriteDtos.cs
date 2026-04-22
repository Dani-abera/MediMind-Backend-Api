namespace MediMind.Application.Features.Favorites;

public record FavoriteDoctorResponseDto(
    Guid FavoriteId,
    Guid DoctorId,
    string DoctorName,
    string Specialization,
    string? ProfileImageUrl,
    DateTime CreatedAt);

public record FavoriteCenterResponseDto(
    Guid FavoriteId,
    Guid CenterId,
    string CenterName,
    string City,
    string? ProfileImageUrl,
    DateTime CreatedAt);
