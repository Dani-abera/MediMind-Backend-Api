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

        var payload = new GeezSendRequest(normalized, message, apiKey);

        using var request = new HttpRequestMessage(HttpMethod.Post, "sms/send")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);

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
            logger.LogWarning(ex, "Geez SMS response could not be parsed. Body={Body}", body);
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Geez SMS HTTP {Status}. Phone={Phone} Body={Body}",
                response.StatusCode, normalized, body);
            return parsed ?? new GeezSendResponse((int)response.StatusCode, body, null);
        }

        // Some gateways return 200 with status != success in JSON
        if (parsed is { Status: not 200 and not 1 })
            logger.LogWarning("Geez SMS reported non-success status {Status}. Msg={Msg}", parsed.Status, parsed.ResponseMsg);

        return parsed;
    }
}

public record GeezSendRequest(
    [property: JsonPropertyName("phone")] string Phone,
    [property: JsonPropertyName("msg")] string Msg,
    [property: JsonPropertyName("token")] string Token);

public record GeezSendResponse(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("response_msg")] string ResponseMsg,
    [property: JsonPropertyName("message_id")] string? MessageId);
