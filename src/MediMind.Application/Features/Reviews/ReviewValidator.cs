using FluentValidation;

namespace MediMind.Application.Features.Reviews;

public class CreateReviewValidator : AbstractValidator<CreateReviewDto>
{
    public CreateReviewValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5.");
        RuleFor(x => x.Comment).MaximumLength(1000).When(x => x.Comment is not null);
        RuleFor(x => x).Must(x => x.ReviewDoctor || x.ReviewCenter)
            .WithMessage("At least one of ReviewDoctor or ReviewCenter must be true.");
    }
}
