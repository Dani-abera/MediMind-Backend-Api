using FluentAssertions;
using MediMind.Application.Features.Doctors;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using NSubstitute;
using Xunit;

namespace MediMind.UnitTests.DoctorFeatures;

public class DoctorPatientAccessTests
{
    private readonly IAppointmentRepository _appointments = Substitute.For<IAppointmentRepository>();

    private DoctorPatientAccessChecker BuildChecker() => new(_appointments);

    private static Appointment MakeAppointment(Guid doctorId, Guid patientId, AppointmentStatus status)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var time = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var appt = Appointment.Book(patientId, doctorId, Guid.NewGuid(), date, time, 30,
            "Checkup", null, 30, 2);
        if (status == AppointmentStatus.Confirmed) appt.Approve(Guid.NewGuid());
        else if (status == AppointmentStatus.Completed)
        {
            appt.Approve(Guid.NewGuid());
            appt.MarkInProgress();
            appt.MarkCompleted();
        }
        return appt;
    }

    [Fact]
    public async Task HasAccess_WithConfirmedAppointment_ReturnsTrue()
    {
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appt = MakeAppointment(doctorId, patientId, AppointmentStatus.Confirmed);

        _appointments.GetByPatientAsync(patientId, Arg.Any<CancellationToken>())
            .Returns([appt]);

        var checker = BuildChecker();
        var result = await checker.HasAccessAsync(doctorId, patientId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasAccess_WithCompletedAppointment_ReturnsTrue()
    {
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appt = MakeAppointment(doctorId, patientId, AppointmentStatus.Completed);

        _appointments.GetByPatientAsync(patientId, Arg.Any<CancellationToken>())
            .Returns([appt]);

        var checker = BuildChecker();
        var result = await checker.HasAccessAsync(doctorId, patientId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasAccess_WhenDoctorHasNoAppointmentWithPatient_ReturnsFalse()
    {
        var doctorId = Guid.NewGuid();
        var otherDoctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appt = MakeAppointment(otherDoctorId, patientId, AppointmentStatus.Confirmed);

        _appointments.GetByPatientAsync(patientId, Arg.Any<CancellationToken>())
            .Returns([appt]);

        var checker = BuildChecker();
        var result = await checker.HasAccessAsync(doctorId, patientId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasAccess_WithCancelledAppointmentOnly_ReturnsFalse()
    {
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appt = MakeAppointment(doctorId, patientId, AppointmentStatus.Confirmed);
        appt.Cancel(Guid.NewGuid(), "Patient request", 0);

        _appointments.GetByPatientAsync(patientId, Arg.Any<CancellationToken>())
            .Returns([appt]);

        var checker = BuildChecker();
        var result = await checker.HasAccessAsync(doctorId, patientId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasAccess_WithPendingAppointmentOnly_ReturnsFalse()
    {
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        // Pending status — never approved
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var time = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var appt = Appointment.Book(patientId, doctorId, Guid.NewGuid(), date, time, 30,
            "Checkup", null, 30, 2);

        _appointments.GetByPatientAsync(patientId, Arg.Any<CancellationToken>())
            .Returns([appt]);

        var checker = BuildChecker();
        var result = await checker.HasAccessAsync(doctorId, patientId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasAccess_WithNoAppointmentsAtAll_ReturnsFalse()
    {
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        _appointments.GetByPatientAsync(patientId, Arg.Any<CancellationToken>())
            .Returns([]);

        var checker = BuildChecker();
        var result = await checker.HasAccessAsync(doctorId, patientId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureAccess_WhenNoValidAppointment_ThrowsForbiddenException()
    {
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        _appointments.GetByPatientAsync(patientId, Arg.Any<CancellationToken>())
            .Returns([]);

        var checker = BuildChecker();
        var act = () => checker.EnsureAccessAsync(doctorId, patientId);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task HasAccess_WithInProgressAppointment_ReturnsTrue()
    {
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appt = MakeAppointment(doctorId, patientId, AppointmentStatus.Confirmed);
        appt.MarkInProgress();

        _appointments.GetByPatientAsync(patientId, Arg.Any<CancellationToken>())
            .Returns([appt]);

        var checker = BuildChecker();
        var result = await checker.HasAccessAsync(doctorId, patientId);

        result.Should().BeTrue();
    }
}

public class AppointmentNoteEntityTests
{
    [Fact]
    public void Create_WithValidContent_ShouldCreateNote()
    {
        var note = AppointmentNote.Create(Guid.NewGuid(), Guid.NewGuid(), "Patient has elevated BP.");

        note.Content.Should().Be("Patient has elevated BP.");
        note.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithEmptyContent_ShouldThrowDomainException()
    {
        var act = () => AppointmentNote.Create(Guid.NewGuid(), Guid.NewGuid(), "   ");
        act.Should().Throw<MediMind.Domain.Exceptions.DomainException>();
    }

    [Fact]
    public void Create_WithContentOver5000Chars_ShouldThrowDomainException()
    {
        var content = new string('x', 5001);
        var act = () => AppointmentNote.Create(Guid.NewGuid(), Guid.NewGuid(), content);
        act.Should().Throw<MediMind.Domain.Exceptions.DomainException>()
            .WithMessage("*5000*");
    }

    [Fact]
    public void UpdateContent_WithValidContent_ShouldUpdate()
    {
        var note = AppointmentNote.Create(Guid.NewGuid(), Guid.NewGuid(), "Initial note.");
        note.UpdateContent("Updated note.");
        note.Content.Should().Be("Updated note.");
    }
}

public class PrescriptionTemplateEntityTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateTemplate()
    {
        var template = PrescriptionTemplate.Create(
            Guid.NewGuid(), "Cold package", "For common cold", "URTI",
            "[]", null, null);

        template.Name.Should().Be("Cold package");
        template.UseCount.Should().Be(0);
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrowDomainException()
    {
        var act = () => PrescriptionTemplate.Create(Guid.NewGuid(), "", null, null, "[]", null, null);
        act.Should().Throw<MediMind.Domain.Exceptions.DomainException>();
    }

    [Fact]
    public void IncrementUseCount_ShouldIncrementByOne()
    {
        var template = PrescriptionTemplate.Create(Guid.NewGuid(), "Template A", null, null, "[]", null, null);
        template.IncrementUseCount();
        template.UseCount.Should().Be(1);
    }
}
