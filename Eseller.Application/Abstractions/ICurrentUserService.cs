using Eseller.Domain.Enums;

namespace Eseller.Application.Abstractions;

public interface ICurrentUserService
{
    Guid? AccountId { get; }
    RoleType? RoleType { get; }
    bool IsAuthenticated { get; }
}