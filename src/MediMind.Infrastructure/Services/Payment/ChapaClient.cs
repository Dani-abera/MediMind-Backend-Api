using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using MediMind.Application.Features.Payments;
using Microsoft.Extensions.Options;

namespace MediMind.Infrastructure.Services.Payment;

public sealed class ChapaOptions
{
    public const string SectionName = "Chapa";
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
}

public sealed class ChapaClient(HttpClient httpClient, IOptions<ChapaOptions> options) : IChapaClient
{
    private readonly ChapaOptions _options = options.Value;

    public async Task<ChapaInitializeResponse?> InitializePaymentAsync(ChapaInitializeRequest req, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "transaction/initialize");
        request.Headers.Add("Authorization", $"Chapa-Auth {_options.SecretKey}");
        request.Content = JsonContent.Create(new ChapaInitializeRequestPayload(
            req.Amount,
            req.Currency,
            req.Email,
            req.FirstName,
            req.LastName,
            req.TxRef,
            _options.CallbackUrl,
            _options.ReturnUrl,
            req.Customization));

        using var response = await httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadFromJsonAsync<ChapaInitializeResponseApi>(cancellationToken: ct);
        if (body is null)
            return null;

        return new ChapaInitializeResponse(
            body.Message ?? "Unknown response",
            body.Status ?? "failed",
            new ChapaInitializeData(body.Data?.CheckoutUrl ?? string.Empty));
    }

    public async Task<ChapaVerifyResponse?> VerifyPaymentAsync(string txRef, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"transaction/verify/{txRef}");
        request.Headers.Add("Authorization", $"Chapa-Auth {_options.SecretKey}");

        using var response = await httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadFromJsonAsync<ChapaVerifyResponseApi>(cancellationToken: ct);
        if (body is null || body.Data is null)
            return null;

        return new ChapaVerifyResponse(
            body.Message ?? "Unknown response",
            body.Status ?? "failed",
            new ChapaVerifyData(
                body.Data.TxRef ?? txRef,
                body.Data.FlwRef ?? string.Empty,
                body.Data.Amount,
                body.Data.Currency ?? "ETB",
                body.Data.Status ?? "failed",
                body.Data.PaymentType ?? "unknown"));
    }

    private sealed record ChapaInitializeRequestPayload(
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("first_name")] string FirstName,
        [property: JsonPropertyName("last_name")] string LastName,
        [property: JsonPropertyName("tx_ref")] string TxRef,
        [property: JsonPropertyName("callback_url")] string CallbackUrl,
        [property: JsonPropertyName("return_url")] string ReturnUrl,
        [property: JsonPropertyName("customization")] ChapaCustomization Customization);

    private sealed class ChapaInitializeResponseApi
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("data")] public ChapaInitializeDataApi? Data { get; set; }
    }

    private sealed class ChapaInitializeDataApi
    {
        [JsonPropertyName("checkout_url")] public string? CheckoutUrl { get; set; }
    }

    private sealed class ChapaVerifyResponseApi
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("data")] public ChapaVerifyDataApi? Data { get; set; }
    }

    private sealed class ChapaVerifyDataApi
    {
        [JsonPropertyName("tx_ref")] public string? TxRef { get; set; }
        [JsonPropertyName("flw_ref")] public string? FlwRef { get; set; }
        [JsonPropertyName("amount")] public decimal Amount { get; set; }
        [JsonPropertyName("currency")] public string? Currency { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("payment_type")] public string? PaymentType { get; set; }
    }
}

public sealed class ChapaWebhookValidator : IChapaWebhookValidator
{
    public bool ValidateSignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrEmpty(secret)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computed = Convert.ToHexString(hash).ToLowerInvariant();
        var incoming = (signature?.Trim().ToLowerInvariant()) ?? string.Empty;
        // Constant-time comparison prevents timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(incoming));
    }
}

public sealed class ChapaConfigurationAdapter(IOptions<ChapaOptions> options) : IChapaConfiguration
{
    public string WebhookSecret => options.Value.WebhookSecret;
    public string CallbackUrl => options.Value.CallbackUrl;
    public string ReturnUrl => options.Value.ReturnUrl;
}

// ─── Payment fee configuration ────────────────────────────────────────────────

public sealed class PaymentSettings
{
    public const string SectionName = "PaymentSettings";
    public decimal VatPercent     { get; set; } = 15m;
    public decimal ServicePercent { get; set; } = 2m;
}

public sealed class PaymentConfigurationAdapter(IOptions<PaymentSettings> options) : IPaymentConfiguration
{
    public decimal VatPercent     => options.Value.VatPercent;
    public decimal ServicePercent => options.Value.ServicePercent;
}
