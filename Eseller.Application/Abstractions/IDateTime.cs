namespace Eseller.Application.Abstractions;

public interface IDateTime
{
    DateTime UtcNow { get; }
}