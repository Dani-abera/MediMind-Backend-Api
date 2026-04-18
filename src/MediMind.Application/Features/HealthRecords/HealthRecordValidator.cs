using FluentValidation;

namespace MediMind.Application.Features.HealthRecords;

public class HealthRecordValidator : AbstractValidator<CreateHealthRecordDto>
{
    public HealthRecordValidator()
    {
        RuleFor(x => x.RecordDate)
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Record date cannot be in the future");

        RuleFor(x => x)
            .Must(x => !x.SystolicBp.HasValue || !x.DiastolicBp.HasValue || x.SystolicBp > x.DiastolicBp)
            .WithMessage("Systolic must be higher than diastolic BP");

        RuleFor(x => x)
            .Must(x => x.SystolicBp.HasValue || x.DiastolicBp.HasValue || x.GlucoseLevel.HasValue || x.Weight.HasValue ||
                       x.Height.HasValue || x.Temperature.HasValue || x.HeartRate.HasValue ||
                       x.OxygenSaturation.HasValue || x.RespiratoryRate.HasValue)
            .WithMessage("At least one vital sign measurement is required");

        RuleFor(x => x.SystolicBp).InclusiveBetween(70, 250).When(x => x.SystolicBp.HasValue);
        RuleFor(x => x.DiastolicBp).InclusiveBetween(40, 150).When(x => x.DiastolicBp.HasValue);
        RuleFor(x => x.GlucoseLevel).InclusiveBetween(30, 600).When(x => x.GlucoseLevel.HasValue);
        RuleFor(x => x.Weight).InclusiveBetween(20, 300).When(x => x.Weight.HasValue);
        RuleFor(x => x.Height).InclusiveBetween(50, 250).When(x => x.Height.HasValue);
        RuleFor(x => x.Temperature).InclusiveBetween(35, 43).When(x => x.Temperature.HasValue);
        RuleFor(x => x.HeartRate).InclusiveBetween(30, 250).When(x => x.HeartRate.HasValue);
        RuleFor(x => x.OxygenSaturation).InclusiveBetween(70, 100).When(x => x.OxygenSaturation.HasValue);
        RuleFor(x => x.RespiratoryRate).InclusiveBetween(8, 60).When(x => x.RespiratoryRate.HasValue);
    }
}
