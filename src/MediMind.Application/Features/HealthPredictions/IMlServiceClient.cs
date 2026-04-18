namespace MediMind.Application.Features.HealthPredictions;

/// <summary>
/// Client abstraction for external ML prediction service.
/// </summary>
public interface IMlServiceClient
{
    /// <summary>
    /// Sends computed patient features to ML service and returns prediction output.
    /// </summary>
    Task<MLPredictionResponse?> PredictAsync(MLPredictionRequest request);
}
