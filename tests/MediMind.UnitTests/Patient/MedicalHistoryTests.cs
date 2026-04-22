using FluentAssertions;
using MediMind.Domain.Entities;
using Xunit;

namespace MediMind.UnitTests.PatientFeatures;

public class PatientMedicalHistoryDomainTests
{
    [Fact]
    public void Create_ProducesEmptyDefaults()
    {
        var patientId = Guid.NewGuid();
        var history = PatientMedicalHistory.Create(patientId);

        history.PatientId.Should().Be(patientId);
        history.ChronicConditions.Should().Be("[]");
        history.Allergies.Should().Be("[]");
        history.CurrentMedications.Should().Be("[]");
        history.FamilyHistory.Should().Be("{}");
        history.BloodType.Should().BeNull();
        history.Smoker.Should().BeNull();
        history.AlcoholConsumption.Should().BeNull();
    }

    [Fact]
    public void Update_SetsAllFields()
    {
        var history = PatientMedicalHistory.Create(Guid.NewGuid());
        history.Update("[\"Diabetes\"]", "[{\"allergen\":\"Penicillin\",\"severity\":\"High\"}]",
            "[{\"name\":\"Metformin\",\"dosage\":\"500mg\",\"startedOn\":\"2024-01-01\"}]",
            "A+", true, "Occasional", "{\"father\":\"Hypertension\"}");

        history.ChronicConditions.Should().Be("[\"Diabetes\"]");
        history.BloodType.Should().Be("A+");
        history.Smoker.Should().BeTrue();
        history.AlcoholConsumption.Should().Be("Occasional");
    }

    [Fact]
    public void Clear_ResetsToDefaults()
    {
        var history = PatientMedicalHistory.Create(Guid.NewGuid());
        history.Update("[\"Diabetes\"]", "[]", "[]", "B+", false, "None", "{}");
        history.Clear();

        history.ChronicConditions.Should().Be("[]");
        history.BloodType.Should().BeNull();
        history.Smoker.Should().BeNull();
    }
}
