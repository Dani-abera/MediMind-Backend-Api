using MediMind.Domain.Common;
using MediMind.Domain.Enums;
using MediMind.Domain.Events;

namespace MediMind.Domain.Entities;

/// <summary>
/// Abstract base user — maps to the `users` table.
/// Patient, Doctor, and HealthcareCenterAdmin extend this via TPT (Table-Per-Type).
/// </summary>
public abstract class User : BaseEntity
{
    public string Email { get; protected set; } = string.Empty;
    public string PhoneNumber { get; protected set; } = string.Empty;  // +251XXXXXXXXX
    public string FullName { get; protected set; } = string.Empty;
    public DateOnly DateOfBirth { get; protected set; }
    public Gender Gender { get; protected set; }
    public UserType UserType { get; protected set; }
    public UserStatus Status { get; protected set; } = UserStatus.Pending;
    public string? ProfileImageUrl { get; protected set; }
    public string PasswordHash { get; protected set; } = string.Empty;
    public DateTime? LastLogin { get; protected set; }
    public bool IsVerified { get; protected set; }

    // ─── OTP ─────────────────────────────────────────────────────────────────
    public string? OtpCode { get; private set; }
    public DateTime? OtpExpiresAt { get; private set; }
    public int OtpAttempts { get; private set; }

    protected User() { }

    protected User(
        string email,
        string phoneNumber,
        string fullName,
        DateOnly dateOfBirth,
        Gender gender,
        UserType userType)
    {
        Email = email.ToLowerInvariant();
        PhoneNumber = phoneNumber;
        FullName = fullName;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        UserType = userType;
    }

    // ─── Domain Behaviors ─────────────────────────────────────────────────────

    public void SetPasswordHash(string hash)
    {
        PasswordHash = hash;
        UpdateTimestamp();
    }

    public void GenerateOtp(string code, int expiryMinutes = 5)
    {
        OtpCode = code;
        OtpExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
        OtpAttempts = 0;
        UpdateTimestamp();
    }

    public bool ValidateOtp(string code)
    {
        if (OtpAttempts >= 5)
            return false; // Locked after 5 attempts

        OtpAttempts++;

        if (OtpCode != code || OtpExpiresAt < DateTime.UtcNow)
            return false;

        OtpCode = null;
        OtpExpiresAt = null;
        return true;
    }

    /// <summary>Clears OTP fields and attempt counter after a successful auth path that bypasses <see cref="ValidateOtp"/>.</summary>
    public void ResetOtpState()
    {
        OtpCode = null;
        OtpExpiresAt = null;
        OtpAttempts = 0;
        UpdateTimestamp();
    }

    public void Activate()
    {
        Status = UserStatus.Active;
        IsVerified = true;
        UpdateTimestamp();
        AddDomainEvent(new UserActivatedEvent(Id));
    }

    public void Suspend(string reason)
    {
        Status = UserStatus.Suspended;
        UpdateTimestamp();
    }

    public void RecordLogin()
    {
        LastLogin = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void UpdateProfile(string fullName, string? profileImageUrl)
    {
        FullName = fullName;
        ProfileImageUrl = profileImageUrl;
        UpdateTimestamp();
    }

    public void UpdateIdentityProfile(string fullName, DateOnly dateOfBirth, Gender gender)
    {
        FullName = fullName;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        UpdateTimestamp();
    }

    public void SetProfileImageUrl(string? profileImageUrl)
    {
        ProfileImageUrl = profileImageUrl;
        UpdateTimestamp();
    }

    public bool IsOtpLocked => OtpAttempts >= 5;
    public int Age => DateOnly.FromDateTime(DateTime.Today).Year - DateOfBirth.Year -
                      (DateOnly.FromDateTime(DateTime.Today) < DateOfBirth.AddYears(DateOnly.FromDateTime(DateTime.Today).Year - DateOfBirth.Year) ? 1 : 0);
}
