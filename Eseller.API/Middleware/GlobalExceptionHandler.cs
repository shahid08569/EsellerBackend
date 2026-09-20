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