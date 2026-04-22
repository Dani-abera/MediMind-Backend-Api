using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.Reviews;

public interface IReviewService
{
    Task CreateAsync(Guid appointmentId, Guid patientId, CreateReviewDto dto, CancellationToken ct = default);
    Task<DoctorReviewSummaryDto> GetDoctorReviewsAsync(Guid doctorId, CancellationToken ct = default);
    Task<CenterReviewSummaryDto> GetCenterReviewsAsync(Guid centerId, CancellationToken ct = default);
}

public class ReviewService(
    IReviewRepository reviewRepository,
    IAppointmentRepository appointmentRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IReviewService
{
    public async Task CreateAsync(Guid appointmentId, Guid patientId, CreateReviewDto dto, CancellationToken ct = default)
    {
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);

        if (appointment.PatientId != patientId)
            throw new ForbiddenException("You can only review appointments that belong to you.");

        if (appointment.Status != AppointmentStatus.Completed)
            throw new DomainException("You can only review a completed appointment.");

        if (dto.Rating < 1 || dto.Rating > 5)
            throw new DomainException("Rating must be between 1 and 5.");

        if (dto.Comment?.Length > 1000)
            throw new DomainException("Comment must not exceed 1000 characters.");

        if (!dto.ReviewDoctor && !dto.ReviewCenter)
            throw new DomainException("At least one of ReviewDoctor or ReviewCenter must be true.");

        if (dto.ReviewDoctor)
        {
            if (await reviewRepository.ExistsForAppointmentAsync(appointmentId, isDoctor: true, ct))
                throw new DomainException("You have already reviewed the doctor for this appointment.");

            var review = Review.CreateDoctorReview(patientId, appointment.DoctorId, appointmentId, dto.Rating, dto.Comment);
            await reviewRepository.AddAsync(review, ct);
        }

        if (dto.ReviewCenter)
        {
            if (await reviewRepository.ExistsForAppointmentAsync(appointmentId, isDoctor: false, ct))
                throw new DomainException("You have already reviewed the center for this appointment.");

            var review = Review.CreateCenterReview(patientId, appointment.CenterId, appointmentId, dto.Rating, dto.Comment);
            await reviewRepository.AddAsync(review, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<DoctorReviewSummaryDto> GetDoctorReviewsAsync(Guid doctorId, CancellationToken ct = default)
    {
        var reviews = await reviewRepository.GetByDoctorIdAsync(doctorId, 10, ct);
        var avg = await reviewRepository.GetAverageRatingForDoctorAsync(doctorId, ct);
        var count = await reviewRepository.GetReviewCountForDoctorAsync(doctorId, ct);

        var dtos = await MapReviewsAsync(reviews, ct);
        return new DoctorReviewSummaryDto(doctorId, Math.Round(avg, 1), count, dtos);
    }

    public async Task<CenterReviewSummaryDto> GetCenterReviewsAsync(Guid centerId, CancellationToken ct = default)
    {
        var reviews = await reviewRepository.GetByCenterIdAsync(centerId, 10, ct);
        var avg = await reviewRepository.GetAverageRatingForCenterAsync(centerId, ct);
        var count = await reviewRepository.GetReviewCountForCenterAsync(centerId, ct);

        var dtos = await MapReviewsAsync(reviews, ct);
        return new CenterReviewSummaryDto(centerId, Math.Round(avg, 1), count, dtos);
    }

    private async Task<IReadOnlyList<ReviewResponseDto>> MapReviewsAsync(IReadOnlyList<Review> reviews, CancellationToken ct)
    {
        var result = new List<ReviewResponseDto>();
        foreach (var r in reviews)
        {
            var patient = await userRepository.GetByIdAsync(r.PatientId, ct);
            result.Add(new ReviewResponseDto(
                r.Id, r.PatientId, patient?.FullName ?? "Unknown",
                r.DoctorId, r.CenterId, r.AppointmentId,
                r.Rating, r.Comment, r.CreatedAt));
        }
        return result;
    }
}
