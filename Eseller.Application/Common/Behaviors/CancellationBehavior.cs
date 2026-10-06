using System.Reflection;
using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Common.Behaviors;

/// <summary>
/// Client abort: convert cancel/SQL-abort into Result CANCELLED — do NOT rethrow (VS debugger crash).
/// Only matches real cancel exceptions — never swallow unrelated errors just because the token was aborted.
/// </summary>
public sealed class CancellationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex) when (IsCancelException(ex))
        {
            if (TryCreateCancelledResponse(out var cancelled))
                return cancelled;

            return default!;
        }
    }

    /// <summary>
    /// True only for OperationCanceled / TaskCanceled / SQL "Operation cancelled by user".
    /// Do NOT use cancellationToken.IsCancellationRequested alone — that swallowed real faults while debugging.
    /// </summary>
    private static bool IsCancelException(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException!)
        {
            if (ex is OperationCanceledException or TaskCanceledException)
                return true;

            var msg = ex.Message ?? string.Empty;
            if (msg.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("Operation canceled by user", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool TryCreateCancelledResponse(out TResponse response)
    {
        response = default!;
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            response = (TResponse)(object)Result.Failure(
                "Request was cancelled by the client.",
                "REQUEST_CANCELLED");
            return true;
        }

        if (responseType.IsGenericType
            && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];
            var failure = typeof(Result)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(Result.Failure)
                            && m.IsGenericMethodDefinition
                            && m.GetParameters().Length == 2);
            var generic = failure.MakeGenericMethod(valueType);
            response = (TResponse)generic.Invoke(
                null,
                new object?[] { "Request was cancelled by the client.", "REQUEST_CANCELLED" })!;
            return true;
        }

        return false;
    }
}
