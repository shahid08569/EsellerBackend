using Eseller.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json;

namespace Eseller.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Client navigated away / request aborted — not a server fault.
        if (exception is OperationCanceledException or TaskCanceledException)
        {
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = 499; // Client Closed Request
            }
            return true;
        }

        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";

        var response = new
        {
            error = "An internal error occurred. Please try again later.",
            errorCode = "INTERNAL_ERROR"
        };

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}