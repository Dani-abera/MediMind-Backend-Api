using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediMind.Domain.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QRCoder;

namespace MediMind.Infrastructure.Services.Prescriptions;

public class QrCodeService(
    IOptions<PrescriptionVerificationOptions> options,
    IConfiguration configuration,
    ILogger<QrCodeService> logger) : IQrCodeService
{
    private byte[] GetKey()
    {
        var secret = options.Value.HmacSecret;
        if (string.IsNullOrWhiteSpace(secret))
            secret = configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning("Prescription HMAC secret is not configured; QR verification will fail.");
            secret = "medimind-prescription-fallback-secret-change-me";
        }

        return Encoding.UTF8.GetBytes(secret);
    }

    public string ComputeVerificationRef(Guid prescriptionId, DateOnly issueDate)
    {
        var msg = $"{prescriptionId:N}{issueDate:yyyy-MM-dd}";
        using var hmac = new HMACSHA256(GetKey());
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(msg));
        return Base64UrlEncode(hash);
    }

    public string BuildQrPayloadString(Guid prescriptionId, DateOnly issueDate)
    {
        var payload = new QrPayload(
            prescriptionId.ToString("D"),
            ComputeVerificationRef(prescriptionId, issueDate),
            "MediMind");
        var json = JsonSerializer.Serialize(payload);
        return Base64UrlEncode(Encoding.UTF8.GetBytes(json));
    }

    public string GenerateQrCodeBase64(string qrTextContent)
    {
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(qrTextContent, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(qrData);
        var bytes = png.GetGraphic(4);
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }

    public bool VerifyQrToken(Guid prescriptionId, DateOnly issueDate, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;
        var expected = ComputeVerificationRef(prescriptionId, issueDate);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(token.Trim()));
    }

    private static string Base64UrlEncode(byte[] data)
    {
        var b64 = Convert.ToBase64String(data);
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed record QrPayload(string pid, string @ref, string iss);
}
