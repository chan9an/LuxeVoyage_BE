namespace Shared.Events;

// Published by AI.API when the toxicity model gives the comment a clean bill of health.
// Hotel.API consumes this and flips IsApproved = true so the review goes live.
public interface IReviewApprovedEvent
{
    Guid ReviewId { get; }
    Guid HotelId { get; }
    float ToxicityScore { get; }
}
