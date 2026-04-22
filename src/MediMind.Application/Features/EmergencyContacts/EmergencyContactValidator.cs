using FluentValidation;

namespace MediMind.Application.Features.EmergencyContacts;

public class CreateEmergencyContactValidator : AbstractValidator<CreateEmergencyContactDto>
{
    public CreateEmergencyContactValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20)
            .Matches(@"^\+?[0-9]{7,20}$").WithMessage("PhoneNumber must be a valid phone number.");
    }
}

public class UpdateEmergencyContactValidator : AbstractValidator<UpdateEmergencyContactDto>
{
    public UpdateEmergencyContactValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20)
            .Matches(@"^\+?[0-9]{7,20}$").WithMessage("PhoneNumber must be a valid phone number.");
    }
}
