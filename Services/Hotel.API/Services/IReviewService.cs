using Hotel.API.Entities;

namespace Hotel.API.Services;

public interface IReviewService
{
    // Submit a new review — validates the booking, then queues it for AI moderation
    Task<(bool Success, string Error, ReviewEntity? Review)> SubmitReviewAsync(
        Guid hotelId, string userId, string guestName, string guestEmail,
        Guid bookingId, int rating, string comment);

    // Returns only approved reviews for a given hotel (what the public sees)
    Task<IEnumerable<ReviewEntity>> GetApprovedReviewsAsync(Guid hotelId);

    // Called by the AI consumers to flip the approval state
    Task MarkApprovedAsync(Guid reviewId, float toxicityScore);
    Task MarkRejectedAsync(Guid reviewId, float toxicityScore);

    // Check if a user has already reviewed a specific hotel
    Task<bool> HasUserReviewedAsync(Guid hotelId, string userId);

    // Check if a user has a confirmed booking for a hotel (gate for review submission)
    Task<bool> HasConfirmedBookingAsync(Guid hotelId, string userId);
}
