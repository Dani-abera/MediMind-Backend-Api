using FluentAssertions;
using MediMind.Application.Auth;
using MediMind.Application.Features.Admin;
using MediMind.Application.Features.Auth;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace MediMind.UnitTests.Auth;

// ─── Patient Auth Tests ───────────────────────────────────────────────────────

public class PatientAuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPatientRepository _patients = Substitute.For<IPatientRepository>();
    private readonly IOtpVerificationRepository _otpRepo = Substitute.For<IOtpVerificationRepository>();
    private readonly IPasswordService _passwords = Substitute.For<IPasswordService>();
    private readonly IOtpService _otpService = Substitute.For<IOtpService>();
    private readonly ISmsService _sms = Substitute.For<ISmsService>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IHostEnvironment _env = Substitute.For<IHostEnvironment>();

    private PatientAuthService BuildService(bool testOtpEnabled = false, string testOtpCode = "000000")
    {
        var opts = Options.Create(new TestOtpOptions { Enabled = testOtpEnabled, Code = testOtpCode });
        _env.EnvironmentName.Returns("Development");
        return new PatientAuthService(_users, _patients, _otpRepo, _passwords, _otpService, _sms,
            _authService, _currentUser, _uow, opts, _env);
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyExists_ThrowsDomainException()
    {
        _users.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var svc = BuildService();

        var act = () => svc.RegisterAsync(
            new RegisterPatientRequest("a@b.com", "+251911000001", "Test", DateOnly.Parse("1990-01-01"), Gender.Male, "Pass1234"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Register_WhenPhoneAlreadyExists_ThrowsDomainException()
    {
        _users.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _users.ExistsByPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var svc = BuildService();

        var act = () => svc.RegisterAsync(
            new RegisterPatientRequest("new@b.com", "+251911000001", "Test", DateOnly.Parse("1990-01-01"), Gender.Male, "Pass1234"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*already registered*");
    }

    [Fact]
    public async Task Register_WithValidData_CreatesPatientAndSendsOtp()
    {
        _users.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _users.ExistsByPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _otpService.GenerateOtp().Returns("123456");
        _passwords.HashPassword(Arg.Any<string>()).Returns("hashed");
        var svc = BuildService();

        var result = await svc.RegisterAsync(
            new RegisterPatientRequest("new@b.com", "+251911000001", "Test User", DateOnly.Parse("1990-01-01"), Gender.Male, "Pass1234"),
            CancellationToken.None);

        result.Message.Should().Contain("OTP");
        await _patients.Received(1).AddAsync(Arg.Any<Patient>(), Arg.Any<CancellationToken>());
        await _sms.Received(1).SendOtpAsync("+251911000001", "123456", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyOtp_WhenOtpNotFound_ThrowsDomainException()
    {
        _otpRepo.GetLatestActiveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ReturnsNull();
        var svc = BuildService();

        var act = () => svc.VerifyOtpAsync("+251911000001", "123456", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*OTP not found*");
    }

    [Fact]
    public async Task VerifyOtp_WithTestOtp_BypassesOtpRepository()
    {
        _users.GetByPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNull();
        _passwords.HashPassword(Arg.Any<string>()).Returns("hashed");
        _authService.GenerateAuthTokensAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(("access", "refresh"));
        var svc = BuildService(testOtpEnabled: true, testOtpCode: "000000");

        var result = await svc.VerifyOtpAsync("+251911000001", "000000", CancellationToken.None);

        await _otpRepo.DidNotReceive().GetLatestActiveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        result.AccessToken.Should().Be("access");
    }
}

// ─── Doctor Auth Tests ────────────────────────────────────────────────────────

public class DoctorAuthServiceTests
{
    private readonly IDoctorRepository _doctors = Substitute.For<IDoctorRepository>();
    private readonly IOtpVerificationRepository _otpRepo = Substitute.For<IOtpVerificationRepository>();
    private readonly IOtpService _otpService = Substitute.For<IOtpService>();
    private readonly ISmsService _sms = Substitute.For<ISmsService>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IHostEnvironment _env = Substitute.For<IHostEnvironment>();

    private DoctorAuthService BuildService(bool testOtpEnabled = false, string testOtpCode = "000000")
    {
        var opts = Options.Create(new TestOtpOptions { Enabled = testOtpEnabled, Code = testOtpCode });
        _env.EnvironmentName.Returns("Development");
        return new DoctorAuthService(_doctors, _otpRepo, _otpService, _sms, _authService, _uow, opts, _env);
    }

    [Fact]
    public async Task RequestOtp_WhenBadgeNotFound_ThrowsDomainException()
    {
        _doctors.GetByBadgeNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNull();
        var svc = BuildService();

        var act = () => svc.RequestOtpAsync("999999", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*badge number not found*");
    }

    [Fact]
    public async Task RequestOtp_WhenBadgeFound_SendsOtp()
    {
        var doctor = new Doctor("d@doc.local", "+251911000002", "Dr. Test",
            DateOnly.Parse("1980-01-01"), Gender.Male, "General", "123456", 5);
        _doctors.GetByBadgeNumberAsync("123456", Arg.Any<CancellationToken>()).Returns(doctor);
        _otpService.GenerateOtp().Returns("654321");
        var svc = BuildService();

        await svc.RequestOtpAsync("123456", CancellationToken.None);

        await _sms.Received(1).SendOtpAsync(doctor.PhoneNumber, "654321", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyOtp_WithTestOtp_BypassesOtpRepository()
    {
        var doctor = new Doctor("d@doc.local", "+251911000002", "Dr. Test",
            DateOnly.Parse("1980-01-01"), Gender.Male, "General", "123456", 5);
        doctor.VerifyLicense();
        _doctors.GetByBadgeNumberAsync("123456", Arg.Any<CancellationToken>()).Returns(doctor);
        _authService.GenerateAuthTokensAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(("access_tok", "refresh_tok"));
        var svc = BuildService(testOtpEnabled: true, testOtpCode: "000000");

        var result = await svc.VerifyOtpAsync("123456", "000000", CancellationToken.None);

        await _otpRepo.DidNotReceive().GetLatestActiveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        result.AccessToken.Should().Be("access_tok");
        result.UserType.Should().Be("Doctor");
    }
}

// ─── Admin Auth Tests ─────────────────────────────────────────────────────────

public class AdminAuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IDoctorRepository _doctors = Substitute.For<IDoctorRepository>();
    private readonly IPasswordService _passwords = Substitute.For<IPasswordService>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private AdminAuthService BuildService() =>
        new(_users, _doctors, _passwords, _authService, _currentUser, _uow, _auditLogger);

    [Fact]
    public async Task Regiser_WhenEmailTaken_ThrowsDomainException()
    {
        _users.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var svc = BuildService();

        var act = () => svc.RegisterAsync(
            new AdminRegisterRequest("Admin", "a@b.com", "+251911000003", "Pass1234"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Email already registered*");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ThrowsDomainException()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNull();
        var svc = BuildService();

        var act = () => svc.LoginAsync("wrong@email.com", "badpass", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Invalid credentials*");
    }

    [Fact]
    public async Task Login_After5FailedAttempts_ThrowsLockoutException()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNull();
        var svc = BuildService();
        const string email = "locked@admin.com";

        // Exhaust allowed attempts
        for (var i = 0; i < 5; i++)
        {
            try { await svc.LoginAsync(email, "wrong", CancellationToken.None); }
            catch (DomainException) { /* expected */ }
        }

        var act = () => svc.LoginAsync(email, "wrong", CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Too many failed attempts*locked for 15 minutes*");
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokens()
    {
        var admin = new HealthcareCenterAdmin("a@b.com", "+251911000003", "Admin",
            DateOnly.Parse("1985-01-01"), Gender.NotMentioned, null, "Owner");
        admin.SetPasswordHash("hashed");
        admin.Activate();

        _users.GetByEmailAsync("a@b.com", Arg.Any<CancellationToken>()).Returns(admin);
        _passwords.VerifyPassword("Pass1234", "hashed").Returns(true);
        _authService.GenerateAuthTokensAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(("at", "rt"));
        var svc = BuildService();

        var result = await svc.LoginAsync("a@b.com", "Pass1234", CancellationToken.None);

        result.AccessToken.Should().Be("at");
        result.UserType.Should().Be("Admin");
    }

    [Fact]
    public async Task CreateDoctor_WhenCallerIsNotAdmin_ThrowsForbiddenException()
    {
        var patient = new Patient("p@p.com", "+251911000004", "Patient",
            DateOnly.Parse("1990-01-01"), Gender.Male);
        _currentUser.UserId.Returns(patient.Id);
        _users.GetByIdAsync(patient.Id, Arg.Any<CancellationToken>()).Returns(patient);
        var svc = BuildService();

        var act = () => svc.CreateDoctorAsync(
            new CreateDoctorRequest("Dr. X", "+251911000005", "Cardiology", 5),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
