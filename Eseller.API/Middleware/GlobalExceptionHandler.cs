using Eseller.API.Filters;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using System.Data.Common;

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
        // Client navigated away / request aborted — never treat as a server fault.
        if (RequestAbortedExceptionFilter.IsClientAbort(httpContext, exception)
            || exception is OperationCanceledException or TaskCanceledException
            || IsCancelException(exception))
        {
            if (!httpContext.Response.HasStarted)
                httpContext.Response.StatusCode = StatusCodes.Status204NoContent;

            return true;
        }

        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        if (httpContext.Response.HasStarted)
            return true;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";

        var response = new
        {
            error = "An internal error occurred. Please try again later.",
            errorCode = "INTERNAL_ERROR"
        };

        try
        {
            await httpContext.Response.WriteAsJsonAsync(response, CancellationToken.None);
        }
        catch
        {
            // Connection already gone
        }

        return true;
    }

    private static bool IsCancelException(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException!)
        {
            if (ex is OperationCanceledException or TaskCanceledException)
                return true;

            if (ex is SqlException sql)
            {
                var msg = sql.Message ?? string.Empty;
                if (msg.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Operation canceled by user", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            if (ex is DbException db)
            {
                var msg = db.Message ?? string.Empty;
                if (msg.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Operation canceled by user", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
