using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Ledger.Features.Categories.Entities;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Categories.Pages;

[Authorize]
public class CreateOrEditModel(
    AppDbContext dbContext,
    ILocalizedEntityService localizedEntityService,
    ILanguageService languageService) : PageModel
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ILocalizedEntityService _localizedEntityService = localizedEntityService;
    private readonly ILanguageService _languageService = languageService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Guid? Id { get; set; }
    public List<Category> AvailableParentCategories { get; set; } = new();
    public Dictionary<Guid, string> CategoryTranslations { get; set; } = new();

    // Dictionnaire des traductions pour initialiser le composant UI (code de langue -> valeur traduite)
    public Dictionary<string, string> ExistingTranslations { get; set; } = new();

    // Liste des langues proposées dans la liste déroulante (découvertes dynamiquement)
    public IReadOnlyList<LanguageInfo> SupportedLanguages { get; set; } = new List<LanguageInfo>();

    public class InputModel
    {
        [Required(ErrorMessage = "Le nom de la catégorie est obligatoire.")]
        [StringLength(80)]
        [Display(Name = "Nom de la catégorie")]
        public string Name { get; set; } = string.Empty;

        // Dictionnaire sérialisé en JSON depuis le formulaire contenant les traductions saisies par langue
        public string TranslationsJson { get; set; } = "{}";

        [Required]
        [Display(Name = "Icône visuelle")]
        public string IconName { get; set; } = "bi-tag";

        [Required]
        [Display(Name = "Couleur")]
        public string ColorHex { get; set; } = "#6366f1";

        [Display(Name = "Il s'agit d'un revenu")]
        public bool IsIncome { get; set; } = false;

        [Display(Name = "Revenu d'activité / Salaire (vignette dédiée)")]
        public bool IsSalary { get; set; } = false;

        [Display(Name = "Catégorie parente")]
        public Guid? ParentCategoryId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? parentId = null, CancellationToken cancellationToken = default)
    {
        Id = id;
        SupportedLanguages = _languageService.GetSupportedLanguages();

        AvailableParentCategories = await _dbContext.Categories
            .Where(c => c.ParentCategoryId == null && (!id.HasValue || c.Id != id.Value))
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        CategoryTranslations = await _localizedEntityService.GetTranslationsAsync(
            "Category",
            AvailableParentCategories.Select(c => c.Id),
            "Name",
            cancellationToken: cancellationToken);

        if (id.HasValue)
        {
            var cat = await _dbContext.Categories.FindAsync([id.Value], cancellationToken);
            if (cat == null) return NotFound();

            // Récupérer toutes les traductions existantes en base pour cette catégorie
            ExistingTranslations = await _localizedEntityService.GetAllTranslationsForEntityAsync("Category", cat.Id, "Name", cancellationToken);

            // Si le français n'est pas encore présent dans les traductions spécifiques, y inclure le nom par défaut en base
            if (!ExistingTranslations.ContainsKey("fr") && !string.IsNullOrWhiteSpace(cat.Name))
            {
                ExistingTranslations["fr"] = cat.Name;
            }

            // Déterminer le nom affiché pour la langue active de l'interface
            var currentUiLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            var displayName = ExistingTranslations.TryGetValue(currentUiLang, out var localizedVal) && !string.IsNullOrWhiteSpace(localizedVal)
                ? localizedVal
                : cat.Name;

            Input = new InputModel
            {
                Name = displayName,
                TranslationsJson = JsonSerializer.Serialize(ExistingTranslations),
                IconName = cat.IconName,
                ColorHex = cat.ColorHex,
                IsIncome = cat.IsIncome,
                IsSalary = cat.IsSalary,
                ParentCategoryId = cat.ParentCategoryId
            };
        }
        else if (parentId.HasValue)
        {
            var parent = await _dbContext.Categories.FindAsync([parentId.Value], cancellationToken);
            if (parent != null)
            {
                Input = new InputModel
                {
                    ParentCategoryId = parent.Id,
                    IsIncome = parent.IsIncome,
                    ColorHex = parent.ColorHex,
                    IsSalary = false
                };
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        SupportedLanguages = _languageService.GetSupportedLanguages();

        if (!ModelState.IsValid)
        {
            AvailableParentCategories = await _dbContext.Categories
                .Where(c => c.ParentCategoryId == null && (!id.HasValue || c.Id != id.Value))
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            CategoryTranslations = await _localizedEntityService.GetTranslationsAsync(
                "Category",
                AvailableParentCategories.Select(c => c.Id),
                "Name",
                cancellationToken: cancellationToken);

            ExistingTranslations = DeserializeTranslations(Input.TranslationsJson);

            return Page();
        }

        // Récupérer les traductions soumises
        var submittedTranslations = DeserializeTranslations(Input.TranslationsJson);

        // S'assurer que la langue courante contient bien la valeur du champ principal
        var currentUiLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(Input.Name))
        {
            submittedTranslations[currentUiLang] = Input.Name.Trim();
        }

        // Le nom de base (colonne Name) utilise la traduction 'fr' si disponible, sinon Input.Name
        var canonicalName = submittedTranslations.TryGetValue("fr", out var frVal) && !string.IsNullOrWhiteSpace(frVal)
            ? frVal.Trim()
            : Input.Name.Trim();

        Guid targetId;
        if (id.HasValue)
        {
            var cat = await _dbContext.Categories.FindAsync([id.Value], cancellationToken);
            if (cat == null) return NotFound();

            cat.Name = canonicalName;
            cat.IconName = Input.IconName;
            cat.ColorHex = Input.ColorHex;
            cat.IsIncome = Input.IsIncome;
            cat.IsSalary = Input.IsIncome && Input.IsSalary;
            cat.ParentCategoryId = Input.ParentCategoryId;
            targetId = cat.Id;
        }
        else
        {
            var cat = new Category
            {
                UserId = string.Empty, // Injecté par SaveChangesAsync
                Name = canonicalName,
                IconName = Input.IconName,
                ColorHex = Input.ColorHex,
                IsIncome = Input.IsIncome,
                IsSalary = Input.IsIncome && Input.IsSalary,
                ParentCategoryId = Input.ParentCategoryId
            };
            _dbContext.Categories.Add(cat);
            targetId = cat.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Enregistrer les traductions multilingues dynamiques (toutes langues envoyées ou supportées)
        // Mettre à jour chaque langue présente dans le dictionnaire soumis
        foreach (var (langCode, val) in submittedTranslations)
        {
            if (!string.IsNullOrWhiteSpace(langCode))
            {
                await _localizedEntityService.SetTranslationAsync("Category", targetId, "Name", langCode.Trim(), val?.Trim() ?? string.Empty, cancellationToken);
            }
        }

        return Redirect("/Categories");
    }

    private static Dictionary<string, string> DeserializeTranslations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            // Repli gracieux : un JSON de traductions corrompu retourne un dictionnaire vide pour éviter de bloquer l'édition
            return new Dictionary<string, string>();
        }
    }
}
