namespace Ledger.Features.Security.Context;

/// <summary>
/// Fournit l'accès à l'identifiant de l'utilisateur authentifié courant
/// de manière découplée de HttpContext.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Identifiant unique de l'utilisateur authentifié courant, ou null si anonyme.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Adresse email de l'utilisateur authentifié courant.
    /// </summary>
    string? UserEmail { get; }

    /// <summary>
    /// Indique si la requête courante est exécutée dans un contexte authentifié.
    /// </summary>
    bool IsAuthenticated { get; }
}
