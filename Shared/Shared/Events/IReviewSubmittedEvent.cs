namespace Shared.Events;

// Fired by Hotel.API when a guest submits a review. The AI.API picks this up,
// runs it through the toxicity model, and publishes either Approved or Rejected.
public interface IReviewSubmittedEvent
{
    Guid ReviewId { get; }
    Guid HotelId { get; }
    string UserId { get; }
    Guid BookingId { get; }
    int Rating { get; }
    string Comment { get; }
    string GuestName { get; }
    string HotelName { get; }
    string GuestEmail { get; }
    DateTime SubmittedAt { get; }
}
