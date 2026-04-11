namespace MediMind.Application.Auth;

/// <summary>Non-production only: fixed OTP for local/staging API testing (never enable in production).</summary>
public sealed class TestOtpOptions
{
    public const string SectionName = "Authentication:TestOtp";

    public bool Enabled { get; set; }

    /// <summary>Six-digit code accepted when <see cref="Enabled"/> and host environment is not Production.</summary>
    public string Code { get; set; } = "000000";
}
