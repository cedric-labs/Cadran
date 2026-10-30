using System.ComponentModel.DataAnnotations;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Accounts.Pages;

[Authorize]
public class TypesModel(
    IAccountTypeService accountTypeService,
    AppDbContext dbContext,
    Ledger.Infrastructure.Configuration.ILocalizedEntityService localizedEntityService) : PageModel
{
    public List<TypeItemViewModel> AccountTypes { get; set; } = new();

    public class TypeItemViewModel
    {
        public CustomAccountType Type { get; set; } = new() { Name = string.Empty, Code = string.Empty, UserId = string.Empty };
        public string LocalizedName { get; set; } = string.Empty;
        public int AccountCount { get; set; }
    }

    [BindProperty]
    public TypeInputModel Input { get; set; } = new();

    public class TypeInputModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Le nom du type de compte est obligatoire.")]
        [StringLength(100, ErrorMessage = "Le nom ne doit pas dépasser 100 caractères.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Le code ne doit pas dépasser 50 caractères.")]
        public string? Code { get; set; }

        [StringLength(250, ErrorMessage = "La description ne doit pas dépasser 250 caractères.")]
        public string? Description { get; set; }

        public AccountCategoryGroup CategoryGroup { get; set; } = AccountCategoryGroup.Savings;

        [Required(ErrorMessage = "La couleur est obligatoire.")]
        [RegularExpression("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$", ErrorMessage = "Format hexadécimal invalide (ex: #0dcaf0).")]
        public string ColorHex { get; set; } = "#0dcaf0";

        [Required(ErrorMessage = "L'icône est obligatoire.")]
        public string IconName { get; set; } = "bi-piggy-bank";
    }

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var allTypes = await accountTypeService.GetAllAccountTypesAsync(cancellationToken);

        // Compter les comptes liés à chaque type
        var counts = await dbContext.BankAccounts
            .Where(a => a.CustomAccountTypeId != null)
            .GroupBy(a => a.CustomAccountTypeId)
            .Select(g => new { TypeId = g.Key ?? Guid.Empty, Count = g.Count() })
            .ToDictionaryAsync(g => g.TypeId, g => g.Count, cancellationToken);

        var currentLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        var translations = await localizedEntityService.GetTranslationsAsync(
            "CustomAccountType",
            allTypes.Select(t => t.Id),
            "Name",
            currentLang,
            cancellationToken);

        AccountTypes = allTypes.Select(t => new TypeItemViewModel
        {
            Type = t,
            LocalizedName = translations.TryGetValue(t.Id, out var localized) && !string.IsNullOrWhiteSpace(localized) ? localized : t.Name,
            AccountCount = counts.TryGetValue(t.Id, out var count) ? count : 0
        }).ToList();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync(cancellationToken);
            return Page();
        }

        var currentLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        Guid targetId;
        if (Input.Id.HasValue && Input.Id.Value != Guid.Empty)
        {
            var existing = await accountTypeService.GetByIdAsync(Input.Id.Value, cancellationToken);
            if (existing == null)
            {
                TempData["ErrorMessage"] = "Type de compte introuvable.";
                return RedirectToPage();
            }

            if (string.Equals(currentLang, "fr", StringComparison.Ordinal))
            {
                existing.Name = Input.Name.Trim();
            }
            existing.Code = Input.Code?.Trim();
            existing.Description = Input.Description?.Trim();
            existing.CategoryGroup = Input.CategoryGroup;
            existing.ColorHex = Input.ColorHex.Trim();
            existing.IconName = Input.IconName.Trim();

            await accountTypeService.UpdateAsync(existing, cancellationToken);
            targetId = existing.Id;
            TempData["SuccessMessage"] = $"Le type de compte '{Input.Name.Trim()}' a été mis à jour avec succès.";
        }
        else
        {
            var newType = new CustomAccountType
            {
                UserId = string.Empty,
                Name = Input.Name.Trim(),
                Code = Input.Code?.Trim(),
                Description = Input.Description?.Trim(),
                CategoryGroup = Input.CategoryGroup,
                ColorHex = Input.ColorHex.Trim(),
                IconName = Input.IconName.Trim(),
                IsSystem = false
            };

            await accountTypeService.CreateAsync(newType, cancellationToken);
            targetId = newType.Id;
            TempData["SuccessMessage"] = $"Le type de compte '{newType.Name}' a été créé avec succès.";
        }

        // Sauvegarde de la traduction pour la langue courante
        await localizedEntityService.SetTranslationAsync("CustomAccountType", targetId, "Name", currentLang, Input.Name.Trim(), cancellationToken);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (success, errorMessage) = await accountTypeService.DeleteAsync(id, cancellationToken);
        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage ?? "Impossible de supprimer ce type de compte.";
        }
        else
        {
            TempData["SuccessMessage"] = "Le type de compte a été supprimé avec succès.";
        }

        return RedirectToPage();
    }
}
