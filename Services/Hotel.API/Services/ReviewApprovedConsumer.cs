using MassTransit;
using Shared.Events;

namespace Hotel.API.Services;

/*
 * This consumer sits on the "luxevoyage.review-approved" queue and waits for AI.API to give
 * a review the green light. Once we get the event, we flip IsApproved = true and recalculate
 * the hotel's aggregate rating. The whole thing is async and completely decoupled from the
 * original HTTP request that submitted the review — the guest already got a 202 Accepted
 * response by the time this runs.
 */
public class ReviewApprovedConsumer : IConsumer<IReviewApprovedEvent>
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewApprovedConsumer> _logger;

    public ReviewApprovedConsumer(IReviewService reviewService, ILogger<ReviewApprovedConsumer> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IReviewApprovedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Review {ReviewId} approved by AI (score: {Score})", msg.ReviewId, msg.ToxicityScore);

        try
        {
            await _reviewService.MarkApprovedAsync(msg.ReviewId, msg.ToxicityScore);
        }
        catch (Exception ex)
        {
            // Swallow and log — same pattern as Notification.Worker. A failed DB write here
            // shouldn't crash the consumer or cause the message to be requeued indefinitely.
            _logger.LogError(ex, "Failed to mark review {ReviewId} as approved", msg.ReviewId);
        }
    }
}
