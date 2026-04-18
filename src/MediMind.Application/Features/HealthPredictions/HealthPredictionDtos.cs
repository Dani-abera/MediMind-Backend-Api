namespace MediMind.Application.Features.HealthPredictions;

public record HealthPredictionResponseDto(
    Guid PredictionId,
    Guid PatientId,
    DateOnly PredictionDate,
    TimeOnly PredictionTime,
    decimal DiabetesRisk,
    string DiabetesCategory,
    decimal HypertensionRisk,
    string HypertensionCategory,
    decimal CvdRisk,
    string CvdCategory,
    decimal Confidence,
    string ModelVersion,
    string ConfidenceLevelDescription,
    Dictionary<string, List<string>> ContributingFactors,
    string Recommendations,
    int DataPointsUsed,
    string DataQualityMessage,
    DateTime CreatedAt);

public record HealthPredictionSummaryDto(
    Guid PredictionId,
    DateOnly PredictionDate,
    string DiabetesCategory,
    string HypertensionCategory,
    string CvdCategory,
    decimal Confidence,
    int DataPointsUsed,
    DateTime CreatedAt);

public record PredictionRequestStatusDto(
    bool CanRequest,
    int HealthRecordCount,
    string ConfidenceLevel,
    string Message);

public record MLPredictionRequest(
    int PatientAge,
    string Gender,
    int TotalDaysOfData,
    double? AverageSystolicBp,
    double? AverageDiastolicBp,
    double? BpVariability,
    double? AverageGlucose,
    double? GlucoseStdDev,
    double? GlucoseTrend,
    double? CurrentBmi,
    double? WeightChange,
    double? AverageHeartRate,
    double? AverageTemperature,
    double? MetabolicScore);

public record MLPredictionResponse(
    double DiabetesRisk,
    double HypertensionRisk,
    double CvdRisk,
    double Confidence,
    string ModelVersion,
    Dictionary<string, List<string>> ContributingFactors,
    string Recommendations,
    int DataPointsUsed);
