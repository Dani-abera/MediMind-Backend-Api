using FluentAssertions;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Xunit;

namespace MediMind.UnitTests.Domain;

// ─── Appointment Tests ────────────────────────────────────────────────────────

public class AppointmentTests
{
    private static Appointment CreateValidAppointment(int hoursFromNow = 3)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(hoursFromNow));
        var time = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(hoursFromNow));

        return Appointment.Book(
            patientId: Guid.NewGuid(),
            doctorId: Guid.NewGuid(),
            centerId: Guid.NewGuid(),
            appointmentDate: date,
            appointmentTime: time,
            durationMinutes: 30,
            reasonForVisit: "General checkup",
            symptoms: null,
            advanceBookingDays: 30,
            minimumHoursNotice: 2);
    }

    [Fact]
    public void Book_WithValidData_ShouldCreatePendingAppointment()
    {
        var appointment = CreateValidAppointment();

        appointment.Should().NotBeNull();
        appointment.Status.Should().Be(AppointmentStatus.Pending);
        appointment.DomainEvents.Should().ContainSingle(e =>
            e.GetType().Name == "AppointmentBookedEvent");
    }

    [Fact]
    public void Book_WithLessThan2HoursNotice_ShouldThrowDomainException()
    {
        var action = () => Appointment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(30)),
            TimeOnly.FromDateTime(DateTime.UtcNow.AddMinutes(30)),
            30, "Test", null, 30, 2);

        action.Should().Throw<DomainException>()
            .WithMessage("*2 hours advance notice*");
    }

    [Fact]
    public void Book_MoreThanAdvanceBookingDaysAhead_ShouldThrowDomainException()
    {
        var action = () => Appointment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(91)),
            TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(3)),
            30, "Test", null, 30, 2);

        action.Should().Throw<DomainException>()
            .WithMessage("*30 days*");
    }

    [Fact]
    public void Approve_PendingAppointment_ShouldConfirmAndRaiseDomainEvent()
    {
        var appointment = CreateValidAppointment();
        var adminId = Guid.NewGuid();

        appointment.Approve(adminId);

        appointment.Status.Should().Be(AppointmentStatus.Confirmed);
        appointment.ApprovedBy.Should().Be(adminId);
        appointment.ApprovedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        appointment.DomainEvents.Should().Contain(e =>
            e.GetType().Name == "AppointmentApprovedEvent");
    }

    [Fact]
    public void Approve_AlreadyConfirmedAppointment_ShouldThrowDomainException()
    {
        var appointment = CreateValidAppointment();
        appointment.Approve(Guid.NewGuid());

        var action = () => appointment.Approve(Guid.NewGuid());
        action.Should().Throw<DomainException>()
            .WithMessage("*pending*");
    }

    [Fact]
    public void Cancel_ConfirmedAppointmentWithEnoughNotice_ShouldSucceed()
    {
        var appointment = CreateValidAppointment(hoursFromNow: 10);
        appointment.Approve(Guid.NewGuid());
        appointment.ClearDomainEvents();

        appointment.Cancel(Guid.NewGuid(), "Personal reason", minimumHoursNotice: 2);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        appointment.CancellationReason.Should().Be("Personal reason");
        appointment.DomainEvents.Should().ContainSingle(e =>
            e.GetType().Name == "AppointmentCancelledEvent");
    }

    [Fact]
    public void Cancel_WithinMinimumHoursNotice_ShouldThrowDomainException()
    {
        // Appointment 1.5 hours from now — cannot cancel with 2-hour minimum notice
        var appointment = CreateValidAppointment(hoursFromNow: 3);
        appointment.Approve(Guid.NewGuid());

        // Simulate appointment being close
        var action = () => appointment.Cancel(Guid.NewGuid(), "reason", minimumHoursNotice: 24);
        action.Should().Throw<DomainException>()
            .WithMessage("*24 hours*");
    }

    [Fact]
    public void MarkCompleted_AfterInProgress_ShouldSetCheckoutTime()
    {
        var appointment = CreateValidAppointment();
        appointment.Approve(Guid.NewGuid());
        appointment.MarkInProgress();

        appointment.MarkCompleted();

        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.CheckOutTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        appointment.DomainEvents.Should().Contain(e =>
            e.GetType().Name == "AppointmentCompletedEvent");
    }
}

// ─── Health Record Tests ──────────────────────────────────────────────────────

public class HealthRecordTests
{
    [Fact]
    public void Create_WithValidVitals_ShouldSucceed()
    {
        var record = HealthRecord.Create(
            Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.Today),
            TimeOnly.FromDateTime(DateTime.UtcNow),
            systolicBp: 120,
            diastolicBp: 80,
            glucoseLevel: 95m,
            weight: 70m,
            height: 175m,
            temperature: 36.8m,
            heartRate: 72,
            oxygenSaturation: 98,
            respiratoryRate: 16,
            notes: "Feeling well");

        record.Should().NotBeNull();
        record.SystolicBp.Should().Be(120);
        record.DiastolicBp.Should().Be(80);
        record.Bmi.Should().BeApproximately(22.86m, 0.01m); // 70/(1.75^2)
    }

    [Theory]
    [InlineData(69, 80)]   // Systolic too low
    [InlineData(251, 80)]  // Systolic too high
    public void Create_WithOutOfRangeSystolicBp_ShouldThrowDomainException(
        int systolic, int diastolic)
    {
        var action = () => HealthRecord.Create(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), TimeOnly.MinValue,
            systolic, diastolic, null, null, null, null, null, null, null, null);

        action.Should().Throw<DomainException>()
            .WithMessage("*70 and 250*");
    }

    [Fact]
    public void Create_WithSystolicLowerThanDiastolic_ShouldThrowDomainException()
    {
        var action = () => HealthRecord.Create(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), TimeOnly.MinValue,
            systolicBp: 80,   // Lower than diastolic
            diastolicBp: 90,
            null, null, null, null, null, null, null, null);

        action.Should().Throw<DomainException>()
            .WithMessage("*Systolic BP must be greater than Diastolic BP*");
    }

    [Fact]
    public void Create_WithNullVitals_ShouldSucceed()
    {
        // Patients don't have to fill in everything — all vitals are optional
        var record = HealthRecord.Create(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), TimeOnly.MinValue,
            null, null, null, null, null, null, null, null, null, null);

        record.Should().NotBeNull();
        record.SystolicBp.Should().BeNull();
        record.Bmi.Should().BeNull(); // Cannot calculate without weight and height
    }
}

// ─── Queue Entry Tests ─────────────────────────────────────────────────────────

public class QueueEntryTests
{
    [Fact]
    public void Create_ShouldAssignCorrectQueueNumberAndEstimatedWait()
    {
        var entry = new QueueEntry(
            Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.Today),
            position: 3,
            slotDurationMinutes: 30);

        entry.QueueNumber.Should().Be("Q003");
        entry.EstimatedWaitTimeMinutes.Should().Be(60); // (3-1) * 30
        entry.Status.Should().Be(QueueStatus.Waiting);
    }

    [Fact]
    public void CallPatient_WhenWaiting_ShouldUpdateStatusAndTime()
    {
        var entry = new QueueEntry(Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.Today), 1, 30);

        entry.CallPatient();

        entry.Status.Should().Be(QueueStatus.Called);
        entry.CalledTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CallPatient_WhenAlreadyCalled_ShouldThrowDomainException()
    {
        var entry = new QueueEntry(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), 1, 30);
        entry.CallPatient();

        var action = () => entry.CallPatient();
        action.Should().Throw<DomainException>().WithMessage("*waiting*");
    }
}

// ─── DoctorSchedule Tests ─────────────────────────────────────────────────────

public class DoctorScheduleTests
{
    [Fact]
    public void Create_WithEndTimeBeforeStartTime_ShouldThrowDomainException()
    {
        var action = () => new DoctorSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            ["Monday", "Tuesday"],
            startTime: new TimeOnly(17, 0),
            endTime: new TimeOnly(9, 0),   // End before start!
            slotDuration: 30);

        action.Should().Throw<DomainException>()
            .WithMessage("*End time must be after start time*");
    }

    [Fact]
    public void GetAvailableSlots_ShouldExcludeBookedTimes()
    {
        var schedule = new DoctorSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            ["Monday"],
            new TimeOnly(9, 0), new TimeOnly(12, 0),
            slotDuration: 60);

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(
            ((int)DayOfWeek.Monday - (int)DateTime.Today.DayOfWeek + 7) % 7 + 7));
        var bookedTimes = new[] { new TimeOnly(10, 0) };

        var available = schedule.GetAvailableSlots(date, bookedTimes);

        available.Should().Contain(new TimeOnly(9, 0));
        available.Should().NotContain(new TimeOnly(10, 0)); // Booked
        available.Should().Contain(new TimeOnly(11, 0));
    }
}
