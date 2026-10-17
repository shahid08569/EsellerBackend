using Eseller.Application.Abstractions;
using Eseller.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Eseller.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? AccountId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? _httpContextAccessor.HttpContext?.User?
                .FindFirst("sub")?.Value;

            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public RoleType? RoleType
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?
                .FindFirst(ClaimTypes.Role)?.Value
                ?? _httpContextAccessor.HttpContext?.User?
                .FindFirst("role")?.Value;

            return Enum.TryParse<RoleType>(claim, out var role) ? role : null;
        }
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}