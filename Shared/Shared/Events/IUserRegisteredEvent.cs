namespace Shared.Events;

public interface IUserRegisteredEvent
{
    string UserId { get; }
    string Email { get; }
    string FirstName { get; }
    string LastName { get; }
}
