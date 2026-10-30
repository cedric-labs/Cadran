using System.ComponentModel.DataAnnotations;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.Merchants.Services;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Merchants.Pages;

[Authorize]
public class IndexModel(
    IMerchantService merchantService,
    AppDbContext dbContext,
    Ledger.Infrastructure.Configuration.ILocalizedEntityService localizedEntityService) : PageModel
{
    public List<MerchantItemViewModel> Merchants { get; set; } = new();
    public List<Category> ParentCategories { get; set; } = new();
    public Dictionary<Guid, string> CategoryTranslations { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty]
    public MerchantInputModel Input { get; set; } = new();

    public class MerchantItemViewModel
    {
        public Merchant Merchant { get; set; } = new() { Name = string.Empty, NormalizedName = string.Empty, UserId = string.Empty };
        public int TransactionCount { get; set; }
    }

    public class MerchantInputModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Le nom du tiers est obligatoire.")]
        [StringLength(150, ErrorMessage = "Le nom ne doit pas dépasser 150 caractères.")]
        public string Name { get; set; } = string.Empty;

        public Guid? DefaultCategoryId { get; set; }

        public string ColorHex { get; set; } = "#6366f1";
        public string IconName { get; set; } = "bi-shop";
    }

    public Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        return LoadDataAsync(cancellationToken);
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var allMerchants = await merchantService.GetAllMerchantsAsync(cancellationToken);
        var counts = await merchantService.GetTransactionCountsByMerchantAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var term = SearchTerm.Trim();
            allMerchants = allMerchants.Where(m => m.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || (m.DefaultCategory != null && m.DefaultCategory.Name.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        Merchants = allMerchants.Select(m => new MerchantItemViewModel
        {
            Merchant = m,
            TransactionCount = counts.TryGetValue(m.Id, out var count) ? count : 0
        }).OrderByDescending(x => x.TransactionCount).ThenBy(x => x.Merchant.Name).ToList();

        ParentCategories = await dbContext.Categories
            .Include(c => c.SubCategories)
            .Where(c => c.ParentCategoryId == null)
            .OrderBy(c => c.IsIncome)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var allCategories = await dbContext.Categories.ToListAsync(cancellationToken);
        CategoryTranslations = await localizedEntityService.GetTranslationsAsync(
            "Category",
            allCategories.Select(c => c.Id),
            "Name",
            cancellationToken: cancellationToken);
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await LoadDataAsync(cancellationToken);
            return Page();
        }

        if (Input.Id.HasValue && Input.Id.Value != Guid.Empty)
        {
            await merchantService.UpdateAsync(
                Input.Id.Value,
                Input.Name,
                Input.DefaultCategoryId,
                Input.IconName,
                Input.ColorHex,
                cancellationToken);
            TempData["SuccessMessage"] = $"Le tiers « {Input.Name} » a été mis à jour avec succès.";
        }
        else
        {
            var merchant = await merchantService.GetOrCreateByNameAsync(Input.Name, Input.DefaultCategoryId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(Input.ColorHex) || !string.IsNullOrWhiteSpace(Input.IconName))
            {
                await merchantService.UpdateAsync(merchant.Id, merchant.Name, Input.DefaultCategoryId, Input.IconName, Input.ColorHex, cancellationToken);
            }
            TempData["SuccessMessage"] = $"Le tiers « {Input.Name} » a été enregistré.";
        }

        return RedirectToPage(new { SearchTerm });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var merchant = await merchantService.GetByIdAsync(id, cancellationToken);
        if (merchant == null)
        {
            return NotFound();
        }

        var name = merchant.Name;
        await merchantService.DeleteAsync(id, cancellationToken);
        TempData["SuccessMessage"] = $"Le tiers « {name} » a été supprimé et détaché des opérations.";

        return RedirectToPage(new { SearchTerm });
    }

    public async Task<IActionResult> OnPostMergeAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default)
    {
        if (sourceId == targetId)
        {
            TempData["ErrorMessage"] = "Impossible de fusionner un tiers avec lui-même.";
            return RedirectToPage(new { SearchTerm });
        }

        var source = await merchantService.GetByIdAsync(sourceId, cancellationToken);
        var target = await merchantService.GetByIdAsync(targetId, cancellationToken);

        if (source == null || target == null)
        {
            return NotFound();
        }

        var sourceName = source.Name;
        var targetName = target.Name;
        var count = await merchantService.MergeMerchantsAsync(sourceId, targetId, cancellationToken);

        TempData["SuccessMessage"] = $"« {sourceName} » a été fusionné vers « {targetName} » ({count} opérations réassignées).";
        return RedirectToPage(new { SearchTerm });
    }
}
