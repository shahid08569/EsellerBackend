using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Eseller.Application.Common.Behaviors;

public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int ThresholdMilliseconds = 500;

    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > ThresholdMilliseconds)
        {
            _logger.LogWarning("Long running request: {RequestName} ({ElapsedMilliseconds}ms)",
                typeof(TRequest).Name, stopwatch.ElapsedMilliseconds);
        }

        return response;
    }
}