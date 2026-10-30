using System.Text.Json;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Crée le paramètre applicatif listant les devises configurables lorsqu'il est absent.
/// </summary>
internal sealed class SupportedCurrenciesSettingInitializer(TimeProvider timeProvider)
{
    private const string SettingKey = "Currencies:Supported";

    /// <summary>
    /// Enregistre la liste par défaut des devises si le paramètre n'existe pas encore ; une valeur existante n'est jamais modifiée.
    /// </summary>
    public async Task EnsureAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var existing = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == SettingKey, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            return;
        }

        context.AppSettings.Add(new AppSettingEntity
        {
            Key = SettingKey,
            Value = JsonSerializer.Serialize(CurrencyService.DefaultCurrencies),
            Description = "Liste des devises monétaires configurables du système",
            UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
