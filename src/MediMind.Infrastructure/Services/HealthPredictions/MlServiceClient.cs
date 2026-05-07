using System.Net.Http.Json;
using MediMind.Application.Features.HealthPredictions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediMind.Infrastructure.Services.HealthPredictions;

/// <summary>
/// Typed HTTP client for Python ML prediction service.
/// </summary>
public class MlServiceClient(
    HttpClient httpClient,
    IOptions<MlServiceOptions> options,
    ILogger<MlServiceClient> logger) : IMlServiceClient
{
    /// <inheritdoc />
    public async Task<MLPredictionResponse?> PredictAsync(MLPredictionRequest request)
    {
        try
        {
            var endpoint = "/ml/predict";
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(request)
            };

            if (!string.IsNullOrWhiteSpace(options.Value.ApiKey))
                requestMessage.Headers.Add("X-Api-Key", options.Value.ApiKey);

            var response = await httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("ML prediction request failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<MLPredictionResponse>();
            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "ML service unavailable while requesting prediction.");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "ML prediction request timed out.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected ML prediction client error.");
            return null;
        }
    }
}

/// <summary>
/// Configuration options for external ML service.
/// </summary>
public class MlServiceOptions
{
    public const string SectionName = "MlService";

    public string BaseUrl { get; set; } = "http://localhost:5001";
    public int TimeoutSeconds { get; set; } = 30;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelDirectory { get; set; } = "models";
}
