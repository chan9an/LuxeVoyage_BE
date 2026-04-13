namespace Shared.Events;

public interface IEmailVerificationRequestedEvent
{
    string UserId { get; }
    string Email { get; }
    string FirstName { get; }
    string Otp { get; }
}
