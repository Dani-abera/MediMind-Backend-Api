using MediMind.Domain.Enums;

namespace MediMind.Application.Features.MedicalHistory;

public record MedicalHistoryResponseDto(
    Guid HistoryId,
    Guid PatientId,
    string ChronicConditions,
    string Allergies,
    string CurrentMedications,
    string? BloodType,
    bool? Smoker,
    AlcoholConsumptionLevel? AlcoholConsumption,
    string FamilyHistory,
    DateTime UpdatedAt);

public record UpsertMedicalHistoryDto(
    string ChronicConditions,
    string Allergies,
    string CurrentMedications,
    string? BloodType,
    bool? Smoker,
    AlcoholConsumptionLevel? AlcoholConsumption,
    string FamilyHistory);
