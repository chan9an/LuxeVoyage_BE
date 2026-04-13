namespace Shared.Events;

// Published by AI.API when the model flags the comment as toxic.
// Hotel.API consumes this and marks the review as rejected so it never surfaces publicly.
public interface IReviewRejectedEvent
{
    Guid ReviewId { get; }
    Guid HotelId { get; }
    string UserId { get; }
    string GuestEmail { get; }
    float ToxicityScore { get; }
}
