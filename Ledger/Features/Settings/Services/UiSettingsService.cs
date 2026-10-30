using System.Globalization;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ledger.Features.Settings.Services;

/// <summary>
/// Service d'accès et de persistance des préférences visuelles et réglages d'interface.
/// </summary>
public sealed class UiSettingsService(
    AppDbContext dbContext,
    ILogger<UiSettingsService> logger,
    TimeProvider timeProvider,
    Ledger.Features.Security.Context.IDatabaseKeyHolder? keyHolder = null) : IUiSettingsService
{
    private const string FontSizeKey = "Ui.FontSize";
    private const string DefaultDisplayModeKey = "Ui.DefaultDisplayMode";
    private const string DefaultPeriodKey = "Ui.DefaultPeriod";
    private const string TransactionsPageSizeKey = "Ui.TransactionsPageSize";
    private const string ShowKpiCentsKey = "Ui.ShowKpiCents";
    private const string DuplicateToleranceDaysKey = "Imports.DuplicateToleranceDays";
    private const string SalaryMonthStartDayKey = "Ui.SalaryMonthStartDay";

    private static readonly UiFont FallbackFont = new()
    {
        Id = Guid.Empty,
        Name = "Inter",
        FontFamilyCss = "'Inter', system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif",
        GoogleFontUrl = null,
        IsActive = true,
        IsSystem = true,
        DisplayOrder = 1
    };

    private bool IsDatabaseLocked => keyHolder != null && keyHolder.IsDatabaseEncrypted && !keyHolder.IsUnlocked;

    public async Task<UiFont> GetActiveFontAsync(CancellationToken cancellationToken = default)
    {
        if (IsDatabaseLocked)
        {
            return FallbackFont;
        }

        try
        {
            var font = await dbContext.UiFonts.FirstOrDefaultAsync(f => f.IsActive, cancellationToken).ConfigureAwait(false);
            if (font != null) return font;

            // Si aucune police active, prendre la première
            font = await dbContext.UiFonts.OrderBy(f => f.DisplayOrder).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (font != null)
            {
                font.IsActive = true;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return font;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Impossible de charger la police active depuis la base de données. Utilisation de la police par défaut.");
        }

        return FallbackFont;
    }

    public async Task<string> GetFontSizeAsync(CancellationToken cancellationToken = default)
    {
        if (IsDatabaseLocked)
        {
            return "14px";
        }

        return await GetStringSettingAsync(FontSizeKey, "14px", "Impossible de charger la taille de police depuis la base.", cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetDefaultDisplayModeAsync(CancellationToken cancellationToken = default)
    {
        if (IsDatabaseLocked)
        {
            return "compact";
        }

        return await GetStringSettingAsync(DefaultDisplayModeKey, "compact", "Impossible de charger le mode d'affichage par défaut depuis la base.", cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<UiFont>> GetAllFontsAsync(CancellationToken cancellationToken = default)
    {
        if (IsDatabaseLocked)
        {
            return new List<UiFont> { FallbackFont };
        }

        return await dbContext.UiFonts
            .OrderBy(f => f.DisplayOrder)
            .ThenBy(f => f.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetActiveFontAsync(Guid fontId, CancellationToken cancellationToken = default)
    {
        var target = await dbContext.UiFonts.FirstOrDefaultAsync(f => f.Id == fontId, cancellationToken).ConfigureAwait(false);
        if (target == null) return;

        var allFonts = await dbContext.UiFonts.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var f in allFonts)
        {
            f.IsActive = (f.Id == fontId);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Police active définie sur '{FontName}' ({FontId})", target.Name, target.Id);
    }

    public async Task SetFontSizeAsync(string fontSize, CancellationToken cancellationToken = default)
    {
        var validSizes = new[] { "13px", "14px", "15px", "16px" };
        if (!validSizes.Contains(fontSize))
        {
            fontSize = "14px";
        }

        await UpsertSettingAsync(FontSizeKey, fontSize, "Taille de police globale de l'interface", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Taille de police définie sur {FontSize}", fontSize);
    }

    public async Task SetDefaultDisplayModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(mode, "compact", StringComparison.Ordinal) && !string.Equals(mode, "comfort", StringComparison.Ordinal))
        {
            mode = "compact";
        }

        await UpsertSettingAsync(DefaultDisplayModeKey, mode, "Mode d'affichage par défaut des opérations (compact ou comfort)", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Mode d'affichage des opérations par défaut défini sur {DisplayMode}", mode);
    }

    public Task<string> GetDefaultPeriodAsync(CancellationToken cancellationToken = default)
    {
        return GetStringSettingAsync(DefaultPeriodKey, "current-month", "Impossible de charger la période par défaut depuis la base.", cancellationToken);
    }

    public async Task SetDefaultPeriodAsync(string period, CancellationToken cancellationToken = default)
    {
        var validPeriods = new[] { "current-month", "two-weeks", "last-month", "three-months", "six-months", "all" };
        if (!validPeriods.Contains(period))
        {
            period = "current-month";
        }

        await UpsertSettingAsync(DefaultPeriodKey, period, "Période temporelle sélectionnée par défaut à l'ouverture des comptes", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Période par défaut définie sur {Period}", period);
    }

    public Task<int> GetTransactionsPageSizeAsync(CancellationToken cancellationToken = default)
    {
        return GetIntSettingAsync(TransactionsPageSizeKey, 250, size => size > 0, "Impossible de charger le nombre maximum d'opérations depuis la base.", cancellationToken);
    }

    public async Task SetTransactionsPageSizeAsync(int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0) pageSize = 250;

        await UpsertSettingAsync(TransactionsPageSizeKey, pageSize.ToString(CultureInfo.InvariantCulture), "Nombre maximum d'opérations chargées par page (0 = illimité, défaut 250)", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Nombre max d'opérations défini sur {PageSize}", pageSize);
    }

    public async Task<bool> GetShowKpiCentsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var setting = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == ShowKpiCentsKey, cancellationToken).ConfigureAwait(false);
            if (setting != null && bool.TryParse(setting.Value, out var showCents))
            {
                return showCents;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Impossible de charger l'affichage des centimes depuis la base.");
        }

        return true;
    }

    public async Task SetShowKpiCentsAsync(bool showCents, CancellationToken cancellationToken = default)
    {
        await UpsertSettingAsync(ShowKpiCentsKey, showCents.ToString().ToLowerInvariant(), "Indique si les centimes doivent être affichés sur les vignettes de synthèse financière (true/false)", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Affichage des centimes sur les vignettes défini sur {ShowCents}", showCents);
    }

    public Task<int> GetDuplicateToleranceDaysAsync(CancellationToken cancellationToken = default)
    {
        return GetIntSettingAsync(DuplicateToleranceDaysKey, 5, days => days >= 0, "Impossible de charger la tolérance de doublons depuis la base.", cancellationToken);
    }

    public async Task SetDuplicateToleranceDaysAsync(int days, CancellationToken cancellationToken = default)
    {
        var clampedDays = Math.Clamp(days, 0, 30);
        await UpsertSettingAsync(DuplicateToleranceDaysKey, clampedDays.ToString(CultureInfo.InvariantCulture), "Tolérance d'écart en jours pour la détection des doublons d'opérations lors de l'import et du nettoyage", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Tolérance d'écart de doublons définie sur {Days} jours", clampedDays);
    }

    public async Task AddCustomFontAsync(string name, string fontFamilyCss, string? googleFontUrl, CancellationToken cancellationToken = default)
    {
        var maxOrder = await dbContext.UiFonts.Select(f => (int?)f.DisplayOrder).MaxAsync(cancellationToken).ConfigureAwait(false) ?? 0;

        var font = new UiFont
        {
            Name = name.Trim(),
            FontFamilyCss = fontFamilyCss.Trim(),
            GoogleFontUrl = string.IsNullOrWhiteSpace(googleFontUrl) ? null : googleFontUrl.Trim(),
            IsActive = false,
            IsSystem = false,
            DisplayOrder = maxOrder + 1
        };

        dbContext.UiFonts.Add(font);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Nouvelle police ajoutée : '{FontName}'", font.Name);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteFontAsync(Guid fontId, CancellationToken cancellationToken = default)
    {
        var font = await dbContext.UiFonts.FirstOrDefaultAsync(f => f.Id == fontId, cancellationToken).ConfigureAwait(false);
        if (font == null)
        {
            return (false, "Police introuvable.");
        }

        if (font.IsSystem)
        {
            return (false, "Les polices de base fournies par l'application ne peuvent pas être supprimées.");
        }

        if (font.IsActive)
        {
            return (false, "Impossible de supprimer la police active. Veuillez activer une autre police au préalable.");
        }

        dbContext.UiFonts.Remove(font);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Police personnalisée '{FontName}' supprimée.", font.Name);
        return (true, null);
    }

    public async Task<string?> GetTableColumnsConfigAsync(string tableKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var settingKey = $"Ui.TableColumns.{tableKey}";
            var setting = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == settingKey, cancellationToken).ConfigureAwait(false);
            return setting?.Value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Impossible de charger la configuration des colonnes pour {TableKey}", tableKey);
            return null;
        }
    }

    public async Task SetTableColumnsConfigAsync(string tableKey, string configJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var settingKey = $"Ui.TableColumns.{tableKey}";
            await UpsertSettingAsync(settingKey, configJson, $"Configuration des colonnes du tableau {tableKey}", cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Configuration des colonnes mise à jour pour {TableKey}", tableKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erreur lors de l'enregistrement de la configuration des colonnes pour {TableKey}", tableKey);
            throw new InvalidOperationException($"Erreur lors de l'enregistrement de la configuration des colonnes pour {tableKey}", ex);
        }
    }

    public async Task EnsureDefaultFontsAndSettingsAsync(CancellationToken cancellationToken = default)
    {
        var existingCount = await dbContext.UiFonts.CountAsync(cancellationToken).ConfigureAwait(false);
        if (existingCount == 0)
        {
            var defaultFonts = DefaultUiFontCatalog.Create();

            await dbContext.UiFonts.AddRangeAsync(defaultFonts, cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Polices d'interface par défaut initialisées en base de données.");
        }
        else
        {
            // Nettoyage rétrocompatible : si les polices système avaient une URL distante vers Google, les basculer en local
            var obsoleteRemoteSystemFonts = await dbContext.UiFonts.Where(f => f.IsSystem && f.GoogleFontUrl != null).ToListAsync(cancellationToken).ConfigureAwait(false);
            if (obsoleteRemoteSystemFonts.Count > 0)
            {
                foreach (var font in obsoleteRemoteSystemFonts)
                {
                    font.GoogleFontUrl = null;
                }
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // Vérification des réglages de base dans AppSettings
        var fontSize = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == FontSizeKey, cancellationToken).ConfigureAwait(false);
        if (fontSize == null)
        {
            await UpsertSettingAsync(FontSizeKey, "14px", "Taille de police globale de l'interface", cancellationToken).ConfigureAwait(false);
        }

        var displayMode = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == DefaultDisplayModeKey, cancellationToken).ConfigureAwait(false);
        if (displayMode == null)
        {
            await UpsertSettingAsync(DefaultDisplayModeKey, "compact", "Mode d'affichage par défaut des opérations (compact ou comfort)", cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<int> GetSalaryMonthStartDayAsync(CancellationToken cancellationToken = default)
    {
        return GetIntSettingAsync(SalaryMonthStartDayKey, 1, day => day >= 1 && day <= 28, "Erreur lecture SalaryMonthStartDay.", cancellationToken);
    }

    public Task SetSalaryMonthStartDayAsync(int day, CancellationToken cancellationToken = default)
    {
        var clampedDay = Math.Clamp(day, 1, 28);
        return UpsertSettingAsync(SalaryMonthStartDayKey, clampedDay.ToString(CultureInfo.InvariantCulture), "Jour de début du mois budgétaire / salarial (1 à 28)", cancellationToken);
    }

    public (DateTime StartDate, DateTime EndDate) GetSalaryMonthBounds(DateTime refDate, int startDay)
    {
        if (startDay <= 1 || startDay > 28)
        {
            var start = new DateTime(refDate.Year, refDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(refDate.Year, refDate.Month, DateTime.DaysInMonth(refDate.Year, refDate.Month), 0, 0, 0, DateTimeKind.Utc);
            return (start, end);
        }

        DateTime periodStart;
        if (refDate.Day >= startDay)
        {
            periodStart = new DateTime(refDate.Year, refDate.Month, startDay, 0, 0, 0, DateTimeKind.Utc);
        }
        else
        {
            periodStart = new DateTime(refDate.Year, refDate.Month, startDay, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        }
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        return (periodStart, periodEnd);
    }

    private async Task<string> GetStringSettingAsync(string key, string defaultValue, string warningMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            var setting = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken).ConfigureAwait(false);
            if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
            {
                return setting.Value;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "{WarningMessage}", warningMessage);
        }

        return defaultValue;
    }

    private async Task<int> GetIntSettingAsync(string key, int defaultValue, Func<int, bool> predicate, string warningMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            var setting = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken).ConfigureAwait(false);
            if (setting != null && int.TryParse(setting.Value, CultureInfo.InvariantCulture, out var value) && predicate(value))
            {
                return value;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "{WarningMessage}", warningMessage);
        }

        return defaultValue;
    }

    private async Task UpsertSettingAsync(string key, string value, string description, CancellationToken cancellationToken = default)
    {
        var setting = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken).ConfigureAwait(false);
        if (setting == null)
        {
            dbContext.AppSettings.Add(new AppSettingEntity
            {
                Key = key,
                Value = value,
                Description = description,
                UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
