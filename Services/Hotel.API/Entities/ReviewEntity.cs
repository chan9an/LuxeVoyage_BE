namespace Hotel.API.Entities;

public class ReviewEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Which hotel this review belongs to — FK to HotelEntity
    public Guid HotelId { get; set; }
    public HotelEntity Hotel { get; set; } = null!;

    // The user who wrote it (sub claim from JWT)
    public string UserId { get; set; } = string.Empty;

    // We require a BookingId so we can verify the guest actually stayed here.
    // Without this check anyone could spam reviews for hotels they never visited.
    public Guid BookingId { get; set; }

    public int Rating { get; set; }          // 1–5 stars
    public string Comment { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;

    // Starts as false — the AI pipeline flips this to true once the comment passes toxicity check.
    // Reviews with IsApproved = false are never returned to the public-facing endpoints.
    public bool IsApproved { get; set; } = false;

    // Null means still in the AI pipeline. True = clean, False = flagged as toxic.
    public bool? IsRejected { get; set; } = null;

    // The raw probability score the model returned — useful for debugging edge cases later
    public float ToxicityScore { get; set; } = 0f;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
