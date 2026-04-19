namespace MediMind.Infrastructure.Services.Prescriptions;

public class PrescriptionStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Root folder under content root for prescription PDF files.</summary>
    public string PrescriptionPdfPath { get; set; } = "storage/prescriptions/";

    /// <summary>Public base URL prefix for <see cref="PrescriptionPdfPath"/> (e.g. https://api.medimind.et/storage/).</summary>
    public string BaseUrl { get; set; } = "https://api.medimind.et/storage/";
}

public class PrescriptionVerificationOptions
{
    public const string SectionName = "Prescription";

    /// <summary>Secret for HMAC verification tokens. Falls back to Jwt:SecretKey when empty.</summary>
    public string HmacSecret { get; set; } = string.Empty;
}
