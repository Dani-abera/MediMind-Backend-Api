using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace MediMind.Infrastructure.Services.Auth;

// ─── JWT Token Service ────────────────────────────────────────────────────────

public class TokenService(IConfiguration config, MediMindDbContext db) : ITokenService, IJwtService
{
    private readonly string _secretKey = config["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
    private readonly string _issuer = config["Jwt:Issuer"] ?? "MediMind";
    private readonly string _audience = config["Jwt:Audience"] ?? "MediMind";

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
            new(ClaimTypes.Role, userType),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
        };

        if (tenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
            claims.Add(new Claim("center_id", tenantId.Value.ToString()));
        }

        var accessToken = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15), // 15-minute expiry (FR-002)
            signingCredentials: credentials);

        var rawRefreshToken = GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Persist: replace any existing token for this user (atomic delete + insert)
        db.RefreshTokens.Where(r => r.UserId == userId).ExecuteDelete();
        db.RefreshTokens.Add(new RefreshToken(userId, rawRefreshToken, expiresAt));
        db.SaveChanges();

        return (new JwtSecurityTokenHandler().WriteToken(accessToken), rawRefreshToken);
    }

    public async Task<Guid?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var row = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == refreshToken, ct);

        if (row is null) return null;

        // Atomic DELETE — returns 0 if a concurrent request already consumed this token
        var deleted = await db.RefreshTokens
            .Where(r => r.Token == refreshToken)
            .ExecuteDeleteAsync(ct);

        if (deleted == 0) return null;

        return row.IsExpired ? null : row.UserId;
    }

    public async Task RevokeRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        await db.RefreshTokens.Where(r => r.UserId == userId).ExecuteDeleteAsync(ct);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

public class AuthService(ITokenService tokenService) : IAuthService
{
    public Task<(string AccessToken, string RefreshToken)> GenerateAuthTokensAsync(User user, CancellationToken ct = default)
    {
        var tenantId = user is HealthcareCenterAdmin admin && admin.CenterId.HasValue
            ? admin.CenterId.Value
            : (Guid?)null;
        var tokens = tokenService.GenerateTokens(user.Id, user.UserType.ToString(), tenantId);
        return Task.FromResult(tokens);
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
