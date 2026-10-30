using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Services;
using Ledger.Features.Security.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Dédoublonne les types de comptes et statuts, puis garantit les catégories, types de comptes et statuts par défaut de chaque utilisateur existant.
/// Les services absents du conteneur sont ignorés.
/// </summary>
internal static class UserReferenceDataSeeder
{
    /// <summary>
    /// Applique le nettoyage au démarrage puis l'initialisation par utilisateur.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var categorySeedService = serviceProvider.GetService<ICategorySeedService>();
        var accountTypeService = serviceProvider.GetService<IAccountTypeService>();
        var transactionStatusService = serviceProvider.GetService<ITransactionStatusService>();
        var userManager = serviceProvider.GetService<UserManager<ApplicationUser>>();

        // Nettoyage au démarrage des doublons non utilisés
        if (accountTypeService != null)
        {
            await accountTypeService.DeduplicateAccountTypesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (transactionStatusService != null)
        {
            await transactionStatusService.DeduplicateStatusesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (userManager == null)
        {
            return;
        }

        var existingUserIds = await userManager.Users.Select(u => u.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var userId in existingUserIds)
        {
            if (categorySeedService != null)
            {
                await categorySeedService.EnsureDefaultCategoriesForUserAsync(userId, cancellationToken).ConfigureAwait(false);
            }

            if (accountTypeService != null)
            {
                await accountTypeService.EnsureDefaultAccountTypesForUserAsync(userId, cancellationToken).ConfigureAwait(false);
            }

            if (transactionStatusService != null)
            {
                await transactionStatusService.EnsureDefaultStatusesForUserAsync(userId, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
