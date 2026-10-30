using System.Globalization;
using Ledger.Features.Categories.Entities;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Ledger.Features.Categories.Pages;

[Authorize]
public class IndexModel(
    AppDbContext dbContext,
    Ledger.Infrastructure.Configuration.ILocalizedEntityService localizedEntityService,
    Ledger.Features.Categories.Services.ICategorySeedService categorySeedService,
    Ledger.Features.Security.Context.ICurrentUserService currentUserService,
    IStringLocalizer? localizer = null) : PageModel
{
    public IReadOnlyList<Category> ParentCategories { get; set; } = new List<Category>();
    public IReadOnlyList<Category> OrphanCategories { get; set; } = new List<Category>();
    public Dictionary<Guid, string> CategoryTranslations { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? StatusMessageType { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = currentUserService.UserId;
        if (!string.IsNullOrEmpty(currentUserId))
        {
            await categorySeedService.EnsureDefaultCategoriesForUserAsync(currentUserId, cancellationToken);
        }

        var all = await dbContext.Categories
            .Include(c => c.SubCategories)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        ParentCategories = all
            .Where(c => c.ParentCategoryId == null)
            .OrderBy(c => c.IsIncome)
            .ThenBy(c => c.Name, StringComparer.CurrentCulture)
            .ToList();

        var parentIds = ParentCategories.Select(p => p.Id).ToHashSet();
        OrphanCategories = all
            .Where(c => c.ParentCategoryId != null && !parentIds.Contains(c.ParentCategoryId.Value))
            .ToList();

        CategoryTranslations = await localizedEntityService.GetTranslationsAsync("Category", all.Select(c => c.Id), "Name", cancellationToken: cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostRestoreDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = currentUserService.UserId;
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Challenge();
        }

        var restoredCount = await categorySeedService.RestoreDefaultCategoriesAsync(currentUserId, cancellationToken);
        if (restoredCount > 0)
        {
            var pattern = localizer?["Categories.RestoreDefaultsSuccess"]?.Value ?? "{0} catégorie(s) par défaut restaurée(s) avec succès.";
            StatusMessage = string.Format(CultureInfo.CurrentCulture, pattern, restoredCount);
            StatusMessageType = "success";
        }
        else
        {
            StatusMessage = localizer?["Categories.RestoreDefaultsNone"]?.Value ?? "Toutes les catégories par défaut sont déjà présentes dans votre compte.";
            StatusMessageType = "info";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.Categories
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category != null)
        {
            // Détacher les sous-catégories le cas échéant
            foreach (var sub in category.SubCategories)
            {
                sub.ParentCategoryId = null;
            }

            dbContext.Categories.Remove(category);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return RedirectToPage();
    }
}
