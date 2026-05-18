using MediMind.Domain.Common;

namespace MediMind.Domain.Entities;

public class PlatformConfiguration : BaseEntity
{
    public static readonly Guid SingletonId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    public decimal TrialFeeEtb { get; private set; }
    public decimal BasicFeeEtb { get; private set; }
    public decimal PremiumFeeEtb { get; private set; }
    public int MaxAdvanceBookingDays { get; private set; } = 90;
    public int MaxSlotDurationMinutes { get; private set; } = 60;
    public string FeatureFlagsJson { get; private set; } = "{}";
    public bool MaintenanceMode { get; private set; }
    public string MaintenanceMessage { get; private set; } = string.Empty;

    public void Update(
        decimal trialFeeEtb,
        decimal basicFeeEtb,
        decimal premiumFeeEtb,
        int maxAdvanceBookingDays,
        int maxSlotDurationMinutes,
        string featureFlagsJson,
        bool maintenanceMode,
        string maintenanceMessage)
    {
        TrialFeeEtb = trialFeeEtb;
        BasicFeeEtb = basicFeeEtb;
        PremiumFeeEtb = premiumFeeEtb;
        MaxAdvanceBookingDays = maxAdvanceBookingDays;
        MaxSlotDurationMinutes = maxSlotDurationMinutes;
        FeatureFlagsJson = featureFlagsJson;
        MaintenanceMode = maintenanceMode;
        MaintenanceMessage = maintenanceMessage;
        UpdateTimestamp();
    }

    public static PlatformConfiguration CreateDefault() => new()
    {
        Id = SingletonId,
        TrialFeeEtb = 0,
        BasicFeeEtb = 500,
        PremiumFeeEtb = 1500,
        MaxAdvanceBookingDays = 90,
        MaxSlotDurationMinutes = 60,
        FeatureFlagsJson = """{"telemedicine":true,"aiPredictions":true,"chapaPayments":true}""",
        MaintenanceMode = false,
        MaintenanceMessage = string.Empty,
    };
}
