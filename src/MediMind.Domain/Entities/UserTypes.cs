using MediMind.Domain.Common;
using MediMind.Domain.Enums;

namespace MediMind.Domain.Entities;

// ─── Patient ──────────────────────────────────────────────────────────────────

/// <summary>Maps to `patients` table — extends `users` via TPT.</summary>
public class Patient : User
{
    public string? Address { get; private set; }
    public BloodType? BloodType { get; private set; }
    public string? Allergies { get; private set; }
    public string? EmergencyContactName { get; private set; }
    public string? EmergencyContactPhone { get; private set; }
    public string? MedicalHistory { get; private set; }
    public List<string> ChronicConditions { get; private set; } = [];
    public List<string> CurrentMedications { get; private set; } = [];

    // Navigation
    public ICollection<HealthRecord> HealthRecords { get; private set; } = [];
    public ICollection<HealthPrediction> HealthPredictions { get; private set; } = [];
    public ICollection<Appointment> Appointments { get; private set; } = [];
    public ICollection<Payment> Payments { get; private set; } = [];
    public IEnumerable<HealthcareCenter> EnrolledCenters =>
        Appointments
            .Where(a => a.Center is not null)
            .Select(a => a.Center)
            .DistinctBy(c => c.Id);

    private Patient() { }

    public Patient(
        string email,
        string phoneNumber,
        string fullName,
        DateOnly dateOfBirth,
        Gender gender)
        : base(email, phoneNumber, fullName, dateOfBirth, gender, UserType.Patient)
    {
    }

    public void UpdateMedicalProfile(
        string? address,
        BloodType? bloodType,
        string? allergies,
        string? emergencyContactName,
        string? emergencyContactPhone,
        string? medicalHistory,
        List<string>? chronicConditions,
        List<string>? currentMedications)
    {
        Address = address;
        BloodType = bloodType;
        Allergies = allergies;
        EmergencyContactName = emergencyContactName;
        EmergencyContactPhone = emergencyContactPhone;
        MedicalHistory = medicalHistory;
        ChronicConditions = chronicConditions ?? [];
        CurrentMedications = currentMedications ?? [];
        UpdateTimestamp();
    }
}

// ─── Doctor ───────────────────────────────────────────────────────────────────

/// <summary>Maps to `doctors` table — extends `users` via TPT.</summary>
public class Doctor : User
{
    public string BadgeNumber { get; private set; } = string.Empty;
    public string Specialization { get; private set; } = string.Empty;
    public string LicenseNumber { get; private set; } = string.Empty;
    public int YearsOfExperience { get; private set; }
    public string? Qualifications { get; private set; }
    public List<string> LanguagesSpoken { get; private set; } = [];

    // Navigation
    public ICollection<DoctorHealthcareCenter> DoctorHealthcareCenters { get; private set; } = [];
    public ICollection<DoctorSchedule> Schedules { get; private set; } = [];
    public ICollection<Appointment> Appointments { get; private set; } = [];
    public ICollection<Prescription> Prescriptions { get; private set; } = [];

    private Doctor() { }

    public Doctor(
        string email,
        string phoneNumber,
        string fullName,
        DateOnly dateOfBirth,
        Gender gender,
        string specialization,
        string badgeNumber,
        int yearsOfExperience)
        : base(email, phoneNumber, fullName, dateOfBirth, gender, UserType.Doctor)
    {
        BadgeNumber = badgeNumber;
        Specialization = specialization;
        LicenseNumber = badgeNumber;
        YearsOfExperience = yearsOfExperience;
    }

    public void UpdateProfessionalInfo(
        string specialization,
        int yearsOfExperience,
        string? qualifications,
        List<string>? languagesSpoken)
    {
        Specialization = specialization;
        YearsOfExperience = yearsOfExperience;
        Qualifications = qualifications;
        LanguagesSpoken = languagesSpoken ?? [];
        UpdateTimestamp();
    }
}

// ─── Healthcare Center Admin ──────────────────────────────────────────────────

/// <summary>Maps to `healthcare_center_admins` table.</summary>
public class HealthcareCenterAdmin : User
{
    public Guid? CenterId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string? Department { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Navigation
    public HealthcareCenter? Center { get; private set; }
    public ICollection<Appointment> ApprovedAppointments { get; private set; } = [];

    private HealthcareCenterAdmin() { }

    public HealthcareCenterAdmin(
        string email,
        string phoneNumber,
        string fullName,
        DateOnly dateOfBirth,
        Gender gender,
        Guid? centerId,
        string role,
        string? department = null)
        : base(email, phoneNumber, fullName, dateOfBirth, gender, UserType.Admin)
    {
        CenterId = centerId;
        Role = role;
        Department = department;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdateTimestamp();
    }

    public void AssignCenter(Guid centerId)
    {
        CenterId = centerId;
        UpdateTimestamp();
    }
}
