namespace Shared.Events;

public interface IPasswordResetRequestedEvent
{
    string UserId { get; }
    string Email { get; }
    string FirstName { get; }
    string Otp { get; }
}
