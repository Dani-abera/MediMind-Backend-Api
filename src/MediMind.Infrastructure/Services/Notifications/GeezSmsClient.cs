using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediMind.Infrastructure.Services.Notifications;

public class GeezSmsClient(
    HttpClient httpClient,
    IOptions<GeezSmsOptions> options,
    ILogger<GeezSmsClient> logger)
{
    private readonly GeezSmsOptions _options = options.Value;

    public async Task<GeezSendResponse?> SendSmsAsync(string phone, string message, CancellationToken ct = default)
    {
        var apiKey = string.IsNullOrWhiteSpace(_options.ApiKey)
            ? throw new InvalidOperationException("GeezSms:ApiKey is not configured.")
            : _options.ApiKey;

        var normalized = EthiopiaPhone.Normalize(phone);

        var payload = new GeezSendRequest(normalized, message);

        using var request = new HttpRequestMessage(HttpMethod.Post, "sms/send")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("KEY", apiKey);

        using var response = await httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        GeezSendResponse? parsed = null;
        try
        {
            parsed = System.Text.Json.JsonSerializer.Deserialize<GeezSendResponse>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SMS Ethiopia response could not be parsed. Body={Body}", body);
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "SMS Ethiopia HTTP {Status}. Phone={Phone} Body={Body}",
                response.StatusCode, normalized, body);
            return parsed ?? new GeezSendResponse("error", body);
        }

        // HTTP 2xx = delivery accepted. Log raw body so we can verify the JSON shape.
        logger.LogInformation("SMS Ethiopia accepted. Phone={Phone} Body={Body}", normalized, body);

        // Normalise whatever status string the gateway returns into "success".
        return new GeezSendResponse("success", parsed?.Message ?? body);
    }
}

public record GeezSendRequest(
    [property: JsonPropertyName("msisdn")] string Msisdn,
    [property: JsonPropertyName("text")] string Text);

public record GeezSendResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message);
