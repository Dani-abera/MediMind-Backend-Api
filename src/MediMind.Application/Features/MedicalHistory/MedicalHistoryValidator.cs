using FluentValidation;

namespace MediMind.Application.Features.MedicalHistory;

public class UpsertMedicalHistoryValidator : AbstractValidator<UpsertMedicalHistoryDto>
{
    private static readonly string[] ValidBloodTypes = ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"];
    private static readonly string[] ValidAlcohol = ["None", "Occasional", "Regular"];

    public UpsertMedicalHistoryValidator()
    {
        RuleFor(x => x.ChronicConditions).NotNull().WithMessage("ChronicConditions must be a valid JSON array.");
        RuleFor(x => x.Allergies).NotNull().WithMessage("Allergies must be a valid JSON array.");
        RuleFor(x => x.CurrentMedications).NotNull().WithMessage("CurrentMedications must be a valid JSON array.");
        RuleFor(x => x.FamilyHistory).NotNull().WithMessage("FamilyHistory must be a valid JSON object.");
        RuleFor(x => x.BloodType)
            .Must(bt => bt is null || ValidBloodTypes.Contains(bt))
            .WithMessage($"BloodType must be one of: {string.Join(", ", ValidBloodTypes)}.");
        RuleFor(x => x.AlcoholConsumption)
            .Must(a => a is null || ValidAlcohol.Contains(a))
            .WithMessage($"AlcoholConsumption must be one of: {string.Join(", ", ValidAlcohol)}.");
    }
}
