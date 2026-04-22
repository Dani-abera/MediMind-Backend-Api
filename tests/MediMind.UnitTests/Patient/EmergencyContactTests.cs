using FluentAssertions;
using MediMind.Application.Features.EmergencyContacts;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Exceptions;
using NSubstitute;
using AutoMapper;
using Xunit;

namespace MediMind.UnitTests.PatientFeatures;

public class EmergencyContactServiceTests
{
    private readonly IEmergencyContactRepository _repo = Substitute.For<IEmergencyContactRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();

    private EmergencyContactService BuildService() => new(_repo, _uow, _mapper);

    private static EmergencyContact MakeContact(Guid patientId, bool isPrimary = false) =>
        EmergencyContact.Create(patientId, "Test User", "Spouse", "+251911000001", isPrimary);

    [Fact]
    public async Task CreateAsync_WhenAtMaxContacts_ThrowsDomainException()
    {
        var patientId = Guid.NewGuid();
        _repo.CountByPatientAsync(patientId, default).Returns(3);

        var svc = BuildService();
        var act = () => svc.CreateAsync(patientId, new CreateEmergencyContactDto("Name", "Spouse", "+251911000002", false));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*at most*");
    }

    [Fact]
    public async Task CreateAsync_WhenFirstContact_SetsIsPrimaryRegardlessOfInput()
    {
        var patientId = Guid.NewGuid();
        _repo.CountByPatientAsync(patientId, default).Returns(0);
        _repo.GetByPatientIdAsync(patientId, default).Returns([]);

        EmergencyContact? captured = null;
        await _repo.AddAsync(Arg.Do<EmergencyContact>(c => captured = c), default);
        _mapper.Map<EmergencyContactResponseDto>(Arg.Any<EmergencyContact>())
            .Returns(new EmergencyContactResponseDto(Guid.NewGuid(), patientId, "Name", "Spouse", "+251911000001", true, DateTime.UtcNow));

        var svc = BuildService();
        await svc.CreateAsync(patientId, new CreateEmergencyContactDto("Name", "Spouse", "+251911000001", false));

        captured.Should().NotBeNull();
        captured!.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenContactNotFound_ThrowsNotFoundException()
    {
        var patientId = Guid.NewGuid();
        _repo.GetByIdAsync(Arg.Any<Guid>(), patientId, default).Returns((EmergencyContact?)null);

        var svc = BuildService();
        var act = () => svc.DeleteAsync(patientId, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
