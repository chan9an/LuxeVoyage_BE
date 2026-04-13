using Hotel.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Hotel.API.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(IReviewService reviewService, ILogger<ReviewsController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    // GET /api/reviews/hotel/{hotelId}
    // Public endpoint — returns only approved reviews. No auth needed, anyone can read reviews.
    [HttpGet("hotel/{hotelId:guid}")]
    public async Task<IActionResult> GetReviews(Guid hotelId)
    {
        var reviews = await _reviewService.GetApprovedReviewsAsync(hotelId);
        return Ok(reviews.Select(r => new
        {
            r.Id,
            r.Rating,
            r.Comment,
            r.GuestName,
            r.CreatedAt
        }));
    }

    // GET /api/reviews/hotel/{hotelId}/can-review
    // Tells the frontend whether to show the "Write a Review" button.
    // Returns { canReview: bool, reason: string }
    [Authorize]
    [HttpGet("hotel/{hotelId:guid}/can-review")]
    public async Task<IActionResult> CanReview(Guid hotelId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var alreadyReviewed = await _reviewService.HasUserReviewedAsync(hotelId, userId);
        if (alreadyReviewed)
            return Ok(new { canReview = false, reason = "You have already reviewed this property." });

        return Ok(new { canReview = true, reason = "" });
    }

    // POST /api/reviews
    // Authenticated guests submit a review here. We validate the booking ID and queue
    // the comment for AI moderation — the response is 202 Accepted, not 201 Created,
    // because the review isn't live yet (it's pending the AI pipeline).
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> SubmitReview([FromBody] SubmitReviewRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        var guestName  = User.FindFirstValue(ClaimTypes.Name) ?? "Guest";
        var guestEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var (success, error, review) = await _reviewService.SubmitReviewAsync(
            request.HotelId, userId, guestName, guestEmail,
            request.BookingId, request.Rating, request.Comment);

        if (!success)
            return BadRequest(new { message = error });

        // 202 because the review is queued for moderation, not immediately published
        return Accepted(new
        {
            message  = "Your review has been submitted and is pending moderation.",
            reviewId = review!.Id
        });
    }
}

// Simple request DTO — keeping it inline since it's only used by this controller
public record SubmitReviewRequest(
    Guid HotelId,
    Guid BookingId,
    int Rating,
    string Comment
);
