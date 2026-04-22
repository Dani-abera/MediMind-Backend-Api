using FluentValidation;

namespace MediMind.Application.Features.EmergencyContacts;

public class CreateEmergencyContactValidator : AbstractValidator<CreateEmergencyContactDto>
{
    private static readonly string[] ValidRelationships = ["Spouse", "Parent", "Sibling", "Friend", "Other"];

    public CreateEmergencyContactValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Relationship)
            .NotEmpty()
            .Must(r => ValidRelationships.Contains(r))
            .WithMessage($"Relationship must be one of: {string.Join(", ", ValidRelationships)}.");
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20)
            .Matches(@"^\+?[0-9]{7,20}$").WithMessage("PhoneNumber must be a valid phone number.");
    }
}

public class UpdateEmergencyContactValidator : AbstractValidator<UpdateEmergencyContactDto>
{
    private static readonly string[] ValidRelationships = ["Spouse", "Parent", "Sibling", "Friend", "Other"];

    public UpdateEmergencyContactValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Relationship)
            .NotEmpty()
            .Must(r => ValidRelationships.Contains(r))
            .WithMessage($"Relationship must be one of: {string.Join(", ", ValidRelationships)}.");
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20)
            .Matches(@"^\+?[0-9]{7,20}$").WithMessage("PhoneNumber must be a valid phone number.");
    }
}
