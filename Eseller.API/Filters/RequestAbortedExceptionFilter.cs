using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace Eseller.API.Filters;

/// <summary>
/// Marks client-aborted requests as handled so reload/navigation never surfaces as a fault
/// (OperationCanceledException, TaskCanceledException, or SQL "Operation cancelled by user").
/// </summary>
public sealed class RequestAbortedExceptionFilter : IAsyncExceptionFilter
{
    public Task OnExceptionAsync(ExceptionContext context)
    {
        if (!IsClientAbort(context.HttpContext, context.Exception))
            return Task.CompletedTask;

        context.ExceptionHandled = true;
        if (!context.HttpContext.Response.HasStarted)
            context.Result = new StatusCodeResult(StatusCodes.Status204NoContent);

        return Task.CompletedTask;
    }

    internal static bool IsClientAbort(HttpContext httpContext, Exception exception)
    {
        if (httpContext.RequestAborted.IsCancellationRequested)
            return true;

        for (var ex = exception; ex is not null; ex = ex.InnerException!)
        {
            if (ex is OperationCanceledException or TaskCanceledException)
                return true;

            if (ex is SqlException sql && IsSqlCancel(sql))
                return true;

            if (ex is DbException db && IsDbCancel(db))
                return true;
        }

        return false;
    }

    private static bool IsSqlCancel(SqlException sql)
    {
        var msg = sql.Message ?? string.Empty;
        return msg.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("Operation canceled by user", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDbCancel(DbException db)
    {
        var msg = db.Message ?? string.Empty;
        return msg.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("Operation canceled by user", StringComparison.OrdinalIgnoreCase);
    }
}
