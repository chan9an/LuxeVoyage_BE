using MassTransit;
using Shared.Events;

namespace Hotel.API.Services;

/*
 * Handles the sad path — AI.API flagged the comment as toxic and published IReviewRejectedEvent.
 * We mark the review as rejected in the DB so it stays hidden from the public endpoints.
 * In a future iteration we could also fire a notification to the guest explaining why their
 * review wasn't published, but for now we just silently reject it.
 */
public class ReviewRejectedConsumer : IConsumer<IReviewRejectedEvent>
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewRejectedConsumer> _logger;

    public ReviewRejectedConsumer(IReviewService reviewService, ILogger<ReviewRejectedConsumer> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IReviewRejectedEvent> context)
    {
        var msg = context.Message;
        _logger.LogWarning("Review {ReviewId} rejected by AI (toxicity score: {Score})", msg.ReviewId, msg.ToxicityScore);

        try
        {
            await _reviewService.MarkRejectedAsync(msg.ReviewId, msg.ToxicityScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark review {ReviewId} as rejected", msg.ReviewId);
        }
    }
}
