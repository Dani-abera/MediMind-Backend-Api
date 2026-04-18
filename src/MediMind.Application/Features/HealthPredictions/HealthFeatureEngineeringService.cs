using MediMind.Domain.Entities;

namespace MediMind.Application.Features.HealthPredictions;

/// <summary>
/// Computes machine learning feature vectors from health records.
/// </summary>
public interface IHealthFeatureEngineeringService
{
    /// <summary>
    /// Computes feature payload for ML prediction.
    /// </summary>
    MLPredictionRequest ComputeFeatures(IEnumerable<HealthRecord> records, Patient patient);
}

public class HealthFeatureEngineeringService : IHealthFeatureEngineeringService
{
    /// <inheritdoc />
    public MLPredictionRequest ComputeFeatures(IEnumerable<HealthRecord> records, Patient patient)
    {
        var orderedRecords = records
            .OrderBy(r => r.RecordDate)
            .ThenBy(r => r.RecordTime)
            .ToList();

        var firstDate = orderedRecords.First().RecordDate;
        var totalDays = orderedRecords
            .Select(r => r.RecordDate)
            .Distinct()
            .Count();

        var systolicValues = orderedRecords.Where(r => r.SystolicBp.HasValue).Select(r => (double)r.SystolicBp!.Value).ToList();
        var diastolicValues = orderedRecords.Where(r => r.DiastolicBp.HasValue).Select(r => (double)r.DiastolicBp!.Value).ToList();
        var glucoseValues = orderedRecords.Where(r => r.GlucoseLevel.HasValue).Select(r => (double)r.GlucoseLevel!.Value).ToList();
        var heartRateValues = orderedRecords.Where(r => r.HeartRate.HasValue).Select(r => (double)r.HeartRate!.Value).ToList();
        var temperatureValues = orderedRecords.Where(r => r.Temperature.HasValue).Select(r => (double)r.Temperature!.Value).ToList();
        var weightEntries = orderedRecords.Where(r => r.Weight.HasValue).ToList();

        var latestRecord = orderedRecords.Last();
        var currentBmi = latestRecord.Bmi.HasValue ? (double?)latestRecord.Bmi.Value : null;
        var weightChange = weightEntries.Count < 2
            ? null
            : (double?)(weightEntries.Last().Weight!.Value - weightEntries.First().Weight!.Value);

        var glucoseTrend = CalculateGlucoseTrend(orderedRecords, firstDate);
        var averageGlucose = AverageOrNull(glucoseValues);
        var averageSystolic = AverageOrNull(systolicValues);
        var averageDiastolic = AverageOrNull(diastolicValues);
        var averageHeartRate = AverageOrNull(heartRateValues);
        var metabolicScore = ComputeMetabolicScore(averageGlucose, averageSystolic, currentBmi, averageHeartRate);

        return new MLPredictionRequest(
            PatientAge: CalculateAge(patient.DateOfBirth),
            Gender: patient.Gender.ToString(),
            TotalDaysOfData: totalDays,
            AverageSystolicBp: averageSystolic,
            AverageDiastolicBp: averageDiastolic,
            BpVariability: StandardDeviationOrNull(systolicValues),
            AverageGlucose: averageGlucose,
            GlucoseStdDev: StandardDeviationOrNull(glucoseValues),
            GlucoseTrend: glucoseTrend,
            CurrentBmi: currentBmi,
            WeightChange: weightChange,
            AverageHeartRate: averageHeartRate,
            AverageTemperature: AverageOrNull(temperatureValues),
            MetabolicScore: metabolicScore);
    }

    private static int CalculateAge(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dateOfBirth.Year;
        if (today < dateOfBirth.AddYears(age))
            age--;

        return age;
    }

    private static double? CalculateGlucoseTrend(List<HealthRecord> records, DateOnly firstDate)
    {
        var glucoseSeries = records
            .Where(r => r.GlucoseLevel.HasValue)
            .Select(r => new
            {
                X = r.RecordDate.DayNumber - firstDate.DayNumber,
                Y = (double)r.GlucoseLevel!.Value
            })
            .ToList();

        if (glucoseSeries.Count < 3)
            return null;

        var n = glucoseSeries.Count;
        var sumX = glucoseSeries.Sum(p => (double)p.X);
        var sumY = glucoseSeries.Sum(p => p.Y);
        var sumXY = glucoseSeries.Sum(p => p.X * p.Y);
        var sumX2 = glucoseSeries.Sum(p => p.X * p.X);

        var denominator = n * sumX2 - (sumX * sumX);
        if (Math.Abs(denominator) < double.Epsilon)
            return null;

        return (n * sumXY - sumX * sumY) / denominator;
    }

    private static double? ComputeMetabolicScore(double? glucose, double? systolicBp, double? bmi, double? heartRate)
    {
        if (!glucose.HasValue || !systolicBp.HasValue || !bmi.HasValue || !heartRate.HasValue)
            return null;

        var normalizedGlucose = NormalizeAndClamp(glucose.Value, 70, 600);
        var normalizedBp = NormalizeAndClamp(systolicBp.Value, 70, 250);
        var normalizedBmi = NormalizeAndClamp(bmi.Value, 10, 50);
        var normalizedHr = NormalizeAndClamp(heartRate.Value, 30, 250);

        return (0.3 * normalizedGlucose) +
               (0.3 * normalizedBp) +
               (0.2 * normalizedBmi) +
               (0.2 * normalizedHr);
    }

    private static double NormalizeAndClamp(double value, double min, double max)
    {
        var clamped = Math.Clamp(value, min, max);
        return (clamped - min) / (max - min);
    }

    private static double? AverageOrNull(IReadOnlyCollection<double> values) =>
        values.Count == 0 ? null : values.Average();

    private static double? StandardDeviationOrNull(IReadOnlyCollection<double> values)
    {
        if (values.Count < 2)
            return null;

        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }
}
