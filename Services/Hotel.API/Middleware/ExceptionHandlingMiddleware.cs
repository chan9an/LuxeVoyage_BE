using Hotel.API.Exceptions;
using System.Text.Json;

namespace Hotel.API.Middleware;

/// <summary>
/// Global exception handler — sits in the pipeline and catches any unhandled exception
/// before it reaches the client as a raw 500 stack trace.
///
/// DomainExceptions get mapped to their specific HTTP status codes with a clean JSON body.
/// Everything else becomes a generic 500 so we never leak internal details to the client.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            // Known business logic error — log at Warning level (not Error, it's not a bug)
            _logger.LogWarning("Domain exception: {Message}", ex.Message);
            await WriteJsonResponse(context, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            // Unknown error — log the full exception so we can investigate
            _logger.LogError(ex, "Unhandled exception");
            await WriteJsonResponse(context, 500, "An unexpected error occurred.");
        }
    }

    private static async Task WriteJsonResponse(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode  = statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new { StatusCode = statusCode, Message = message });
        await context.Response.WriteAsync(body);
    }
}
