using MediMind.Domain.Common;

namespace MediMind.Domain.Entities;

public class OtpVerification : BaseEntity
{
    public string PhoneNumber { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public DateTime ExpirationTime { get; private set; }
    public bool IsUsed { get; private set; }

    private OtpVerification() { }

    public OtpVerification(string phoneNumber, string code, string purpose, int expiresInMinutes = 5)
    {
        PhoneNumber = phoneNumber;
        Code = code;
        Purpose = purpose;
        ExpirationTime = DateTime.UtcNow.AddMinutes(expiresInMinutes);
    }

    public bool IsExpired => DateTime.UtcNow > ExpirationTime;

    public bool Matches(string code) => !IsUsed && !IsExpired && Code == code;

    public void MarkUsed()
    {
        IsUsed = true;
        UpdateTimestamp();
    }
}
