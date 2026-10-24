using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Ledger.Features.Security.Context;

/// <summary>
/// Implémentation basée sur IHttpContextAccessor pour extraire les claims de l'utilisateur courant.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? UserId =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserEmail =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
        ?? httpContextAccessor.HttpContext?.User?.Identity?.Name;

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
