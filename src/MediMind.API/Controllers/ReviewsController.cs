using FluentValidation;
using MediMind.API.Attributes;
using MediMind.Application.Features.Reviews;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>Patient reviews of doctors and healthcare centers (post-appointment).</summary>
[ApiController]
[Route("api/v1")]
public class ReviewsController(
    IReviewService reviewService,
    IValidator<CreateReviewDto> createValidator,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Submit a review for a completed appointment (doctor and/or center).</summary>
    [Tags("Patient — Reviews")]
    [HttpPost("appointments/{id:guid}/review")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateReview(Guid id, [FromBody] CreateReviewDto dto, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        await reviewService.CreateAsync(id, currentUser.UserId, dto, ct);
        return NoContent();
    }

    /// <summary>Get average rating and recent 10 reviews for a doctor (public).</summary>
    [Tags("Public — Reviews")]
    [HttpGet("doctors/{id:guid}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DoctorReviewSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctorReviews(Guid id, CancellationToken ct) =>
        Ok(await reviewService.GetDoctorReviewsAsync(id, ct));

    /// <summary>Get average rating and recent 10 reviews for a healthcare center (public).</summary>
    [Tags("Public — Reviews")]
    [HttpGet("healthcare-centers/{id:guid}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CenterReviewSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCenterReviews(Guid id, CancellationToken ct) =>
        Ok(await reviewService.GetCenterReviewsAsync(id, ct));
}
