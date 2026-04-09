using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MediMind.Domain.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace MediMind.Infrastructure.Services.Auth;

// ─── JWT Token Service ────────────────────────────────────────────────────────

public class TokenService(IConfiguration config) : ITokenService
{
    private readonly string _secretKey = config["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
    private readonly string _issuer = config["Jwt:Issuer"] ?? "MediMind";
    private readonly string _audience = config["Jwt:Audience"] ?? "MediMind";

    // In-memory refresh token store — replace with Redis or DB in production
    private static readonly Dictionary<Guid, (string Token, DateTime Expiry)> RefreshTokens = [];

    public (string AccessToken, string RefreshToken) GenerateTokens(
        Guid userId, string userType, Guid? tenantId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("user_type", userType),
        };

        if (tenantId.HasValue)
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));

        var accessToken = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15), // 15-minute expiry (FR-002)
            signingCredentials: credentials);

        var refreshToken = GenerateRefreshToken();
        RefreshTokens[userId] = (refreshToken, DateTime.UtcNow.AddDays(7)); // 7-day expiry

        return (new JwtSecurityTokenHandler().WriteToken(accessToken), refreshToken);
    }

    public bool ValidateRefreshToken(string refreshToken, out Guid userId)
    {
        userId = Guid.Empty;
        var match = RefreshTokens.FirstOrDefault(kvp => kvp.Value.Token == refreshToken);
        if (match.Key == Guid.Empty) return false;
        if (match.Value.Expiry < DateTime.UtcNow)
        {
            RefreshTokens.Remove(match.Key);
            return false;
        }
        userId = match.Key;
        return true;
    }

    public void RevokeRefreshToken(Guid userId) => RefreshTokens.Remove(userId);

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

// ─── Password Service (BCrypt) ────────────────────────────────────────────────

public class PasswordService : IPasswordService
{
    public string HashPassword(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: 10);

    public bool VerifyPassword(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}

// ─── OTP Service ──────────────────────────────────────────────────────────────

public class OtpService : IOtpService
{
    public string GenerateOtp()
    {
        // Cryptographically secure 6-digit OTP
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        var number = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1_000_000;
        return number.ToString("D6");
    }
}

// ─── Current User (from HttpContext JWT claims) ───────────────────────────────

public class CurrentUser(Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : Guid.Empty;

    public string UserType => Principal?.FindFirstValue("user_type") ?? string.Empty;

    public Guid? TenantId => Guid.TryParse(Principal?.FindFirstValue("tenant_id"), out var id)
        ? id : null;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
}
