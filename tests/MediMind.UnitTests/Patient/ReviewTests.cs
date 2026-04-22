using FluentAssertions;
using MediMind.Application.Features.Reviews;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using NSubstitute;
using Xunit;

namespace MediMind.UnitTests.PatientFeatures;

public class ReviewServiceTests
{
    private readonly IReviewRepository _reviewRepo = Substitute.For<IReviewRepository>();
    private readonly IAppointmentRepository _appointmentRepo = Substitute.For<IAppointmentRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private ReviewService BuildService() => new(_reviewRepo, _appointmentRepo, _userRepo, _uow);

    private static Appointment BuildCompletedAppointment(Guid patientId)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var time = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var appt = Appointment.Book(patientId, Guid.NewGuid(), Guid.NewGuid(),
            date, time, 30, "Follow-up", null, 30, 2);
        appt.Approve(Guid.NewGuid());
        appt.MarkInProgress();
        appt.MarkCompleted();
        return appt;
    }

    [Fact]
    public async Task CreateAsync_WhenAppointmentNotFound_ThrowsNotFoundException()
    {
        _appointmentRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((Appointment?)null);

        var svc = BuildService();
        var act = () => svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(),
            new CreateReviewDto(5, null, true, false));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_WhenNotPatientOwner_ThrowsForbiddenException()
    {
        var patientId = Guid.NewGuid();
        var appt = BuildCompletedAppointment(patientId);
        _appointmentRepo.GetByIdAsync(appt.Id).Returns(appt);

        var svc = BuildService();
        var wrongPatient = Guid.NewGuid();
        var act = () => svc.CreateAsync(appt.Id, wrongPatient, new CreateReviewDto(4, null, true, false));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateAsync_WhenAppointmentNotCompleted_ThrowsDomainException()
    {
        var patientId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var time = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var appt = Appointment.Book(patientId, Guid.NewGuid(), Guid.NewGuid(),
            date, time, 30, "Checkup", null, 30, 2);
        _appointmentRepo.GetByIdAsync(appt.Id).Returns(appt);

        var svc = BuildService();
        var act = () => svc.CreateAsync(appt.Id, patientId, new CreateReviewDto(4, null, true, false));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*completed*");
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicateDoctorReview_ThrowsDomainException()
    {
        var patientId = Guid.NewGuid();
        var appt = BuildCompletedAppointment(patientId);
        _appointmentRepo.GetByIdAsync(appt.Id).Returns(appt);
        _reviewRepo.ExistsForAppointmentAsync(appt.Id, true, default).Returns(true);

        var svc = BuildService();
        var act = () => svc.CreateAsync(appt.Id, patientId, new CreateReviewDto(5, null, ReviewDoctor: true, ReviewCenter: false));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*already reviewed*");
    }
}
