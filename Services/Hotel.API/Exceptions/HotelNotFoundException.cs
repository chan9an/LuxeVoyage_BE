namespace Hotel.API.Exceptions;

/// <summary>
/// Thrown when a hotel lookup returns nothing — maps to 404 Not Found.
/// Use this instead of returning null and checking it in the controller.
/// </summary>
public class HotelNotFoundException : DomainException
{
    public HotelNotFoundException(Guid id)
        : base($"Hotel with ID '{id}' was not found.", 404) { }
}
