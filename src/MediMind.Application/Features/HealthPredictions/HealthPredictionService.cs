using AutoMapper;
using FluentValidation;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.HealthPredictions;

/// <summary>
/// Provides AI prediction orchestration over patient health records.
/// </summary>
public interface IHealthPredictionService
{
    /// <summary>
    /// Requests a new AI health prediction for a patient.
    /// </summary>
    Task<HealthPredictionResponseDto> RequestPredictionAsync(Guid patientId);

    /// <summary>
    /// Retrieves paginated prediction history for a patient.
    /// </summary>
    Task<IEnumerable<HealthPredictionSummaryDto>> GetByPatientIdAsync(Guid patientId, int page, int pageSize);

    /// <summary>
    /// Retrieves the latest prediction for a patient.
    /// </summary>
    Task<HealthPredictionResponseDto?> GetLatestAsync(Guid patientId);

    /// <summary>
    /// Retrieves a prediction by identifier for a patient.
    /// </summary>
    Task<HealthPredictionResponseDto?> GetByIdAsync(Guid predictionId, Guid patientId);

    /// <summary>
    /// Retrieves recent prediction history limited by count.
    /// </summary>
    Task<IEnumerable<HealthPredictionSummaryDto>> GetHistoryAsync(Guid patientId, int count);

    /// <summary>
    /// Gets whether patient has enough data to request AI prediction.
    /// </summary>
    Task<PredictionRequestStatusDto> GetStatusAsync(Guid patientId);

    /// <summary>
    /// Converts data point count into confidence level description.
    /// </summary>
    string GetConfidenceLevelDescription(int dataPoints);
}

public class HealthPredictionService(
    IPatientRepository patientRepository,
    IHealthRecordRepository healthRecordRepository,
    IHealthPredictionRepository healthPredictionRepository,
    IHealthFeatureEngineeringService featureEngineeringService,
    IMlServiceClient mlServiceClient,
    IMapper mapper) : IHealthPredictionService
{
    /// <inheritdoc/>
    public async Task<HealthPredictionResponseDto> RequestPredictionAsync(Guid patientId)
    {
        var patient = await patientRepository.GetByIdAsync(patientId)
            ?? throw new NotFoundException(nameof(Patient), patientId);

        var records = (await healthRecordRepository.GetAllForPredictionAsync(patientId)).ToList();
        if (records.Count < 1)
            throw new ValidationException("Start tracking your health data to get AI predictions");

        var features = featureEngineeringService.ComputeFeatures(records, patient);
        var predictionResponse = await mlServiceClient.PredictAsync(features);

        if (predictionResponse is null)
            throw new ServiceUnavailableException("AI service temporarily unavailable. Please try again later.");

        var prediction = HealthPrediction.Create(
            patientId,
            ToScore(predictionResponse.DiabetesRisk),
            ToScore(predictionResponse.HypertensionRisk),
            ToScore(predictionResponse.CvdRisk),
            ToScore(predictionResponse.Confidence),
            predictionResponse.ModelVersion,
            predictionResponse.ContributingFactors,
            predictionResponse.Recommendations,
            predictionResponse.DataPointsUsed);

        var created = await healthPredictionRepository.CreateAsync(prediction, records.Select(r => r.Id));
        return MapResponse(created);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<HealthPredictionSummaryDto>> GetByPatientIdAsync(Guid patientId, int page, int pageSize)
    {
        var history = await healthPredictionRepository.GetByPatientIdAsync(patientId, page, pageSize);
        return mapper.Map<IEnumerable<HealthPredictionSummaryDto>>(history);
    }

    /// <inheritdoc/>
    public async Task<HealthPredictionResponseDto?> GetLatestAsync(Guid patientId)
    {
        var prediction = await healthPredictionRepository.GetLatestAsync(patientId);
        return prediction is null ? null : MapResponse(prediction);
    }

    /// <inheritdoc/>
    public async Task<HealthPredictionResponseDto?> GetByIdAsync(Guid predictionId, Guid patientId)
    {
        var prediction = await healthPredictionRepository.GetByIdAsync(predictionId, patientId);
        return prediction is null ? null : MapResponse(prediction);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<HealthPredictionSummaryDto>> GetHistoryAsync(Guid patientId, int count)
    {
        var history = await healthPredictionRepository.GetHistoryAsync(patientId, count);
        return mapper.Map<IEnumerable<HealthPredictionSummaryDto>>(history);
    }

    /// <inheritdoc/>
    public async Task<PredictionRequestStatusDto> GetStatusAsync(Guid patientId)
    {
        var count = await healthRecordRepository.GetRecordCountAsync(patientId);
        var confidence = count switch
        {
            <= 6 => "Low",
            <= 29 => "Medium",
            _ => "High"
        };

        var canRequest = count > 0;
        var message = count switch
        {
            0 => "Start tracking your health data to get AI predictions",
            <= 6 => $"Prediction based on {count} days of health data. Track for 30+ days for high confidence results.",
            <= 29 => $"Prediction based on {count} days of health data. Keep tracking to improve confidence.",
            _ => $"Prediction based on {count} days of health data. Data quality is excellent for high confidence results."
        };

        return new PredictionRequestStatusDto(canRequest, count, confidence, message);
    }

    /// <inheritdoc/>
    public string GetConfidenceLevelDescription(int dataPoints) => dataPoints switch
    {
        <= 6 => "Low Confidence",
        <= 29 => "Medium Confidence",
        _ => "High Confidence"
    };

    private HealthPredictionResponseDto MapResponse(HealthPrediction prediction)
    {
        var confidenceDescription = GetConfidenceLevelDescription(prediction.DataPointsUsed);
        var qualityMessage = $"Prediction based on {prediction.DataPointsUsed} days of health data. Track for 30+ days for high confidence results.";

        return new HealthPredictionResponseDto(
            prediction.Id,
            prediction.PatientId,
            prediction.PredictionDate,
            prediction.PredictionTime,
            prediction.DiabetesRisk,
            CategorizeRisk(prediction.DiabetesRisk).ToString(),
            prediction.HypertensionRisk,
            CategorizeRisk(prediction.HypertensionRisk).ToString(),
            prediction.CvdRisk,
            CategorizeRisk(prediction.CvdRisk).ToString(),
            prediction.Confidence,
            prediction.ModelVersion,
            confidenceDescription,
            prediction.ContributingFactors,
            prediction.Recommendations,
            prediction.DataPointsUsed,
            qualityMessage,
            prediction.CreatedAt);
    }

    private static RiskCategory CategorizeRisk(decimal risk)
    {
        if (risk <= 33m)
            return RiskCategory.Low;
        if (risk <= 66m)
            return RiskCategory.Medium;
        return RiskCategory.High;
    }

    private static decimal ToScore(double score)
    {
        var bounded = Math.Clamp(score, 0d, 100d);
        return decimal.Round((decimal)bounded, 2, MidpointRounding.AwayFromZero);
    }
}
