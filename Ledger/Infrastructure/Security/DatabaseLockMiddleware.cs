using Ledger.Features.Security.Context;
using Microsoft.AspNetCore.Http;

namespace Ledger.Infrastructure.Security;

/// <summary>
/// Middleware interceptant toutes les requêtes HTTP lorsque la base de données SQLite
/// est chiffrée et verrouillée, redirigeant automatiquement vers la page de déverrouillage maître.
/// </summary>
public sealed class DatabaseLockMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IDatabaseKeyHolder keyHolder)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Autoriser les ressources statiques et utilitaires
        if (IsStaticAsset(path))
        {
            await next(context);
            return;
        }

        var isUnlockPage = path.StartsWith("/Security/UnlockDatabase", StringComparison.OrdinalIgnoreCase);

        if (keyHolder.IsDatabaseEncrypted && !keyHolder.IsUnlocked)
        {
            if (!isUnlockPage)
            {
                var returnUrl = path + context.Request.QueryString.Value;
                var redirectUrl = string.IsNullOrWhiteSpace(returnUrl) || string.Equals(returnUrl, "/", StringComparison.Ordinal)
                    ? "/Security/UnlockDatabase"
                    : $"/Security/UnlockDatabase?returnUrl={Uri.EscapeDataString(returnUrl)}";

                context.Response.Redirect(redirectUrl);
                return;
            }
        }
        else if (isUnlockPage && keyHolder.IsUnlocked)
        {
            // Si la base est déjà déverrouillée et que l'utilisateur accède à la page d'unlock, rediriger vers l'accueil
            context.Response.Redirect("/");
            return;
        }

        await next(context);
    }

    private static bool IsStaticAsset(string path)
    {
        return path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase);
    }
}
