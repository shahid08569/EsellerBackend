namespace Eseller.Domain.Common;

public interface ISoftDelete
{
    bool IsActive { get; set; }
    DateTime? DeletedAt { get; set; }
}