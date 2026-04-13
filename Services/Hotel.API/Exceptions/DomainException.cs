namespace Hotel.API.Exceptions;

/// <summary>
/// Base class for all domain-level exceptions in Hotel.API.
/// Throwing one of these means "something went wrong with business logic",
/// as opposed to an infrastructure failure like a DB timeout.
/// The global exception middleware catches these and maps them to HTTP responses.
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>The HTTP status code this exception should map to.</summary>
    public int StatusCode { get; }

    protected DomainException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
