namespace Hotel.API.Exceptions;

/// <summary>
/// Thrown when a manager tries to modify a hotel they don't own — maps to 403 Forbidden.
/// Keeps the ownership enforcement logic in one place rather than scattered across controllers.
/// </summary>
public class UnauthorizedOwnerException : DomainException
{
    public UnauthorizedOwnerException(Guid hotelId)
        : base($"You do not have permission to modify hotel '{hotelId}'.", 403) { }
}
