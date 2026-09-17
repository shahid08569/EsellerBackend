using Eseller.Application.Abstractions;

namespace Eseller.Infrastructure.Services;

public class DateTimeService : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}