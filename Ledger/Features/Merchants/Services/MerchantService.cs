using System.Text.RegularExpressions;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.Security.Context;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ledger.Features.Merchants.Services;

/// <summary>
/// Implémentation du service de gestion des marchands / tiers.
/// </summary>
public sealed class MerchantService(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ILogger<MerchantService> logger) : IMerchantService
{

    public Task<List<Merchant>> GetAllMerchantsAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Merchants
            .Include(m => m.DefaultCategory)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Merchant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Merchants
            .Include(m => m.DefaultCategory)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<Merchant?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeMerchantName(name);
        if (string.IsNullOrEmpty(normalized)) return null;

        return await dbContext.Merchants
            .Include(m => m.DefaultCategory)
            .FirstOrDefaultAsync(m => m.NormalizedName == normalized, cancellationToken);
    }

    public async Task<Merchant> GetOrCreateByNameAsync(string name, Guid? defaultCategoryId = null, CancellationToken cancellationToken = default)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Le nom du marchand ne peut pas être vide.", nameof(name));
        }

        var normalized = NormalizeMerchantName(trimmed);
        var existing = await dbContext.Merchants
            .Include(m => m.DefaultCategory)
            .FirstOrDefaultAsync(m => m.NormalizedName == normalized, cancellationToken);

        if (existing != null)
        {
            if (defaultCategoryId.HasValue && !existing.DefaultCategoryId.HasValue)
            {
                existing.DefaultCategoryId = defaultCategoryId.Value;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return existing;
        }

        return await CreateAsync(trimmed, defaultCategoryId, null, null, cancellationToken);
    }

    public async Task<Merchant> CreateAsync(
        string name,
        Guid? defaultCategoryId = null,
        string? iconName = null,
        string? colorHex = null,
        CancellationToken cancellationToken = default)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Le nom du marchand est requis.", nameof(name));
        }

        var normalized = NormalizeMerchantName(trimmed);
        var existing = await dbContext.Merchants
            .FirstOrDefaultAsync(m => m.NormalizedName == normalized, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Un tiers ou marchand avec le nom '{trimmed}' existe déjà.");
        }

        var userId = currentUserService.UserId ?? string.Empty;

        var merchant = new Merchant
        {
            UserId = userId,
            Name = trimmed,
            NormalizedName = normalized,
            DefaultCategoryId = defaultCategoryId,
            IconName = string.IsNullOrWhiteSpace(iconName) ? "bi-shop" : iconName.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(colorHex) ? "#6366f1" : colorHex.Trim()
        };

        dbContext.Merchants.Add(merchant);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Marchand '{Name}' créé avec succès pour l'utilisateur {UserId}.", trimmed, userId);
        return merchant;
    }

    public async Task<Merchant?> UpdateAsync(
        Guid id,
        string name,
        Guid? defaultCategoryId = null,
        string? iconName = null,
        string? colorHex = null,
        CancellationToken cancellationToken = default)
    {
        var merchant = await dbContext.Merchants.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (merchant == null) return null;

        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Le nom du marchand ne peut pas être vide.", nameof(name));
        }

        var normalized = NormalizeMerchantName(trimmed);
        var duplicate = await dbContext.Merchants
            .FirstOrDefaultAsync(m => m.NormalizedName == normalized && m.Id != id, cancellationToken);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"Un tiers ou marchand avec le nom '{trimmed}' existe déjà.");
        }

        merchant.Name = trimmed;
        merchant.NormalizedName = normalized;
        merchant.DefaultCategoryId = defaultCategoryId;

        if (!string.IsNullOrWhiteSpace(iconName))
            merchant.IconName = iconName.Trim();

        if (!string.IsNullOrWhiteSpace(colorHex))
            merchant.ColorHex = colorHex.Trim();

        merchant.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Marchand ID {Id} mis à jour avec le nom '{Name}'.", id, trimmed);
        return merchant;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var merchant = await dbContext.Merchants.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (merchant == null) return false;

        // Détacher les opérations liées
        var linkedTx = await dbContext.Transactions
            .Where(t => t.MerchantId == id)
            .ToListAsync(cancellationToken);

        foreach (var t in linkedTx)
        {
            t.MerchantId = null;
        }

        // Détacher les règles liées
        var linkedRules = await dbContext.CategorizationRules
            .Where(r => r.TargetMerchantId == id)
            .ToListAsync(cancellationToken);

        foreach (var r in linkedRules)
        {
            r.TargetMerchantId = null;
        }

        dbContext.Merchants.Remove(merchant);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Marchand ID {Id} supprimé ({TxCount} opérations et {RulesCount} règles détachées).",
            id, linkedTx.Count, linkedRules.Count);
        return true;
    }

    public async Task<int> MergeMerchantsAsync(Guid sourceMerchantId, Guid targetMerchantId, CancellationToken cancellationToken = default)
    {
        if (sourceMerchantId == targetMerchantId) return 0;

        var source = await dbContext.Merchants.FirstOrDefaultAsync(m => m.Id == sourceMerchantId, cancellationToken);
        var target = await dbContext.Merchants.FirstOrDefaultAsync(m => m.Id == targetMerchantId, cancellationToken);

        if (source == null || target == null) return 0;

        // Réassigner les opérations
        var linkedTx = await dbContext.Transactions
            .Where(t => t.MerchantId == sourceMerchantId)
            .ToListAsync(cancellationToken);

        foreach (var t in linkedTx)
        {
            t.MerchantId = targetMerchantId;
        }

        // Réassigner les règles
        var linkedRules = await dbContext.CategorizationRules
            .Where(r => r.TargetMerchantId == sourceMerchantId)
            .ToListAsync(cancellationToken);

        foreach (var r in linkedRules)
        {
            r.TargetMerchantId = targetMerchantId;
        }

        dbContext.Merchants.Remove(source);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Marchand '{Source}' fusionné vers '{Target}' ({Count} opérations migrées).",
            source.Name, target.Name, linkedTx.Count);

        return linkedTx.Count;
    }

    public Task<Dictionary<Guid, int>> GetTransactionCountsByMerchantAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Transactions
            .Where(t => t.MerchantId != null)
            .GroupBy(t => t.MerchantId)
            .Select(g => new { MerchantId = g.Key ?? Guid.Empty, Count = g.Count() })
            .ToDictionaryAsync(x => x.MerchantId, x => x.Count, cancellationToken);
    }

    public string NormalizeMerchantName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var clean = Regex.Replace(name.Trim(), @"\s+", " ", RegexOptions.None, TimeSpan.FromMilliseconds(250));
        return clean.ToLowerInvariant();
    }
}
