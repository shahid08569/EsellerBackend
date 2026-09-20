using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Eseller.API.Middleware;

public class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
            return false;   // Let next handler process

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        httpContext.Response.ContentType = "application/json";

        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var response = new
        {
            error = "Validation failed.",
            errorCode = "VALIDATION_ERROR",
            errors = errors
        };

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}