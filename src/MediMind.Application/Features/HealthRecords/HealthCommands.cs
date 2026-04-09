using FluentValidation;
using MediatR;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.HealthRecords;

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record HealthRecordDto(
    Guid Id,
    DateOnly RecordDate,
    TimeOnly RecordTime,
    int? SystolicBp,
    int? DiastolicBp,
    decimal? GlucoseLevel,
    decimal? Weight,
    decimal? Height,
    decimal? Bmi,
    decimal? Temperature,
    int? HeartRate,
    int? OxygenSaturation,
    int? RespiratoryRate,
    string? Notes,
    DateTime CreatedAt);

public record HealthPredictionDto(
    Guid Id,
    DateOnly PredictionDate,
    decimal DiabetesRisk,
    string DiabetesCategory,
    decimal HypertensionRisk,
    string HypertensionCategory,
    decimal CvdRisk,
    string CvdCategory,
    decimal Confidence,
    string ConfidenceLabel,
    Dictionary<string, List<string>> ContributingFactors,
    string Recommendations,
    int DataPointsUsed);

// ═══════════════════════════════════════════════════════════════════════════════
// LOG HEALTH DATA
// ═══════════════════════════════════════════════════════════════════════════════

public record LogHealthDataCommand(
    Guid PatientId,
    DateOnly RecordDate,
    TimeOnly RecordTime,
    int? SystolicBp,
    int? DiastolicBp,
    decimal? GlucoseLevel,
    decimal? Weight,
    decimal? Height,
    decimal? Temperature,
    int? HeartRate,
    int? OxygenSaturation,
    int? RespiratoryRate,
    string? Notes
) : IRequest<HealthRecordDto>;

public class LogHealthDataValidator : AbstractValidator<LogHealthDataCommand>
{
    public LogHealthDataValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.RecordDate)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Record date cannot be in the future.");

        // Validate ranges only when values are provided
        When(x => x.SystolicBp.HasValue, () =>
        {
            RuleFor(x => x.SystolicBp).InclusiveBetween(70, 250)
                .WithMessage("Systolic BP must be between 70 and 250 mmHg.");
        });
        When(x => x.DiastolicBp.HasValue, () =>
        {
            RuleFor(x => x.DiastolicBp).InclusiveBetween(40, 150)
                .WithMessage("Diastolic BP must be between 40 and 150 mmHg.");
        });
        When(x => x.SystolicBp.HasValue && x.DiastolicBp.HasValue, () =>
        {
            RuleFor(x => x).Must(x => x.SystolicBp > x.DiastolicBp)
                .WithMessage("Systolic BP must be higher than Diastolic BP.");
        });
        When(x => x.GlucoseLevel.HasValue, () =>
        {
            RuleFor(x => x.GlucoseLevel).InclusiveBetween(30, 600)
                .WithMessage("Glucose level must be between 30 and 600 mg/dL.");
        });
        When(x => x.Temperature.HasValue, () =>
        {
            RuleFor(x => x.Temperature).InclusiveBetween(35, 43)
                .WithMessage("Temperature must be between 35 and 43 °C.");
        });
        When(x => x.HeartRate.HasValue, () =>
        {
            RuleFor(x => x.HeartRate).InclusiveBetween(30, 250)
                .WithMessage("Heart rate must be between 30 and 250 bpm.");
        });
    }
}

public class LogHealthDataHandler(
    IHealthRecordRepository healthRecordRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LogHealthDataCommand, HealthRecordDto>
{
    public async Task<HealthRecordDto> Handle(LogHealthDataCommand request, CancellationToken ct)
    {
        var record = HealthRecord.Create(
            request.PatientId,
            request.RecordDate,
            request.RecordTime,
            request.SystolicBp,
            request.DiastolicBp,
            request.GlucoseLevel,
            request.Weight,
            request.Height,
            request.Temperature,
            request.HeartRate,
            request.OxygenSaturation,
            request.RespiratoryRate,
            request.Notes);

        await healthRecordRepository.AddAsync(record, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return MapToDto(record);
    }

    private static HealthRecordDto MapToDto(HealthRecord r) => new(
        r.Id, r.RecordDate, r.RecordTime,
        r.SystolicBp, r.DiastolicBp, r.GlucoseLevel,
        r.Weight, r.Height, r.Bmi,
        r.Temperature, r.HeartRate, r.OxygenSaturation,
        r.RespiratoryRate, r.Notes, r.CreatedAt);
}

// ═══════════════════════════════════════════════════════════════════════════════
// REQUEST AI PREDICTION
// ═══════════════════════════════════════════════════════════════════════════════

public record RequestPredictionCommand(Guid PatientId) : IRequest<HealthPredictionDto>;

public class RequestPredictionHandler(
    IHealthRecordRepository healthRecordRepository,
    IHealthPredictionRepository predictionRepository,
    IMlPredictionService mlService,
    IPatientRepository patientRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RequestPredictionCommand, HealthPredictionDto>
{
    public async Task<HealthPredictionDto> Handle(RequestPredictionCommand request, CancellationToken ct)
    {
        var patient = await patientRepository.GetByIdAsync(request.PatientId, ct)
                      ?? throw new NotFoundException(nameof(Patient), request.PatientId);

        // Retrieve ALL available health records for this patient
        var records = await healthRecordRepository.GetByPatientAsync(request.PatientId, 365, ct);

        if (!records.Any())
            throw new DomainException("Start tracking your health data to get AI predictions. Enter your first measurements to begin.");

        // Calculate statistical features for the ML model
        var avgSystolic = records.Where(r => r.SystolicBp.HasValue).Select(r => (double)r.SystolicBp!.Value).DefaultIfEmpty(120).Average();
        var avgDiastolic = records.Where(r => r.DiastolicBp.HasValue).Select(r => (double)r.DiastolicBp!.Value).DefaultIfEmpty(80).Average();
        var avgGlucose = records.Where(r => r.GlucoseLevel.HasValue).Select(r => (double)r.GlucoseLevel!.Value).DefaultIfEmpty(90).Average();
        var latestBmi = records.LastOrDefault(r => r.Bmi.HasValue)?.Bmi ?? 22;
        var latestWeight = records.LastOrDefault(r => r.Weight.HasValue)?.Weight ?? 70;
        var firstWeight = records.FirstOrDefault(r => r.Weight.HasValue)?.Weight ?? latestWeight;
        var weightChange = (double)(latestWeight - firstWeight);

        var mlRequest = new MlPredictionRequest(
            AvgSystolicBp: avgSystolic,
            AvgDiastolicBp: avgDiastolic,
            AvgGlucoseLevel: avgGlucose,
            CurrentBmi: (double)(latestBmi),
            WeightChange: weightChange,
            Age: patient.Age,
            Gender: patient.Gender.ToString(),
            DataPointsCount: records.Count);

        // Call Python Flask ML microservice
        var result = await mlService.PredictAsync(mlRequest, ct);

        // Persist prediction
        var prediction = HealthPrediction.Create(
            request.PatientId,
            (decimal)result.DiabetesRisk,
            (decimal)result.HypertensionRisk,
            (decimal)result.CvdRisk,
            (decimal)result.Confidence,
            result.ModelVersion,
            result.ContributingFactors,
            result.Recommendations,
            records.Count);

        await predictionRepository.AddAsync(prediction, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Confidence label based on data quantity
        var confidenceLabel = records.Count switch
        {
            < 7 => "Low Confidence — track for 30+ days for best results",
            < 30 => "Medium Confidence — keep tracking daily",
            _ => "High Confidence — based on comprehensive tracking"
        };

        return new HealthPredictionDto(
            prediction.Id,
            prediction.PredictionDate,
            prediction.DiabetesRisk,
            prediction.DiabetesCategory.ToString(),
            prediction.HypertensionRisk,
            prediction.HypertensionCategory.ToString(),
            prediction.CvdRisk,
            prediction.CvdCategory.ToString(),
            prediction.Confidence,
            confidenceLabel,
            prediction.ContributingFactors,
            prediction.Recommendations,
            prediction.DataPointsUsed);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// GET HEALTH RECORDS HISTORY
// ═══════════════════════════════════════════════════════════════════════════════

public record GetHealthRecordsQuery(Guid PatientId, int Days = 30) : IRequest<IReadOnlyList<HealthRecordDto>>;

public class GetHealthRecordsHandler(IHealthRecordRepository repo)
    : IRequestHandler<GetHealthRecordsQuery, IReadOnlyList<HealthRecordDto>>
{
    public async Task<IReadOnlyList<HealthRecordDto>> Handle(
        GetHealthRecordsQuery request, CancellationToken ct)
    {
        var records = await repo.GetByPatientAsync(request.PatientId, request.Days, ct);
        return records.Select(r => new HealthRecordDto(
            r.Id, r.RecordDate, r.RecordTime,
            r.SystolicBp, r.DiastolicBp, r.GlucoseLevel,
            r.Weight, r.Height, r.Bmi,
            r.Temperature, r.HeartRate, r.OxygenSaturation,
            r.RespiratoryRate, r.Notes, r.CreatedAt
        )).ToList();
    }
}
