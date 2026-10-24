using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.Merchants.Services;
using Ledger.Features.RulesEngine.Entities;
using Ledger.Features.RulesEngine.Services;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.RulesEngine.Pages;

[Authorize]
public class IndexModel(
    AppDbContext dbContext,
    IRulesEngineService rulesEngineService,
    Ledger.Infrastructure.Configuration.ILocalizedEntityService localizedEntityService,
    IMerchantService merchantService) : PageModel
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IRulesEngineService _rulesEngineService = rulesEngineService;
    private readonly Ledger.Infrastructure.Configuration.ILocalizedEntityService _localizedEntityService = localizedEntityService;
    private readonly IMerchantService _merchantService = merchantService;

    public IReadOnlyList<CategorizationRule> Rules { get; set; } = new List<CategorizationRule>();
    public List<Category> AvailableCategories { get; set; } = new();
    public List<Category> ParentCategories { get; set; } = new();
    public List<BankAccount> AvailableAccounts { get; set; } = new();
    public List<Merchant> AvailableMerchants { get; set; } = new();
    public Dictionary<Guid, string> CategoryTranslations { get; set; } = new();

    [BindProperty]
    public RuleInput Input { get; set; } = new();

    public class RuleInput
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Le nom de la règle est requis.")]
        [Display(Name = "Nom de la règle")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "L'expression régulière ou le motif est obligatoire.")]
        [Display(Name = "Motif Regex")]
        public string Pattern { get; set; } = string.Empty;

        [Required(ErrorMessage = "La catégorie cible est obligatoire.")]
        [Display(Name = "Catégorie assignée")]
        public Guid TargetCategoryId { get; set; }

        [Display(Name = "Marchand / Tiers assigné (optionnel)")]
        public Guid? TargetMerchantId { get; set; }

        public string? NewMerchantName { get; set; }

        [Display(Name = "Compte ciblé (optionnel)")]
        public Guid? TargetBankAccountId { get; set; }

        [Display(Name = "Priorité (1 = prioritaire)")]
        public int Priority { get; set; } = 10;

        [Display(Name = "Montant min (€)")]
        public decimal? MinAmount { get; set; }

        [Display(Name = "Montant max (€)")]
        public decimal? MaxAmount { get; set; }

        [Display(Name = "Décaler la date budgétaire sur le mois suivant")]
        public bool ShiftToNextMonth { get; set; } = false;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        await LoadViewDataAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(Input.Pattern))
        {
            try
            {
                _ = new Regex(Input.Pattern.Trim(), RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
            }
            catch (ArgumentException)
            {
                // La syntaxe invalide de l'expression régulière est capturée pour être restituée sous forme d'erreur de formulaire
                ModelState.AddModelError("Input.Pattern", "Le motif d'expression régulière (Regex) est syntaxiquement invalide.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadViewDataAsync(cancellationToken);
            return Page();
        }

        Guid? targetMerchantId = Input.TargetMerchantId;
        if (!targetMerchantId.HasValue && !string.IsNullOrWhiteSpace(Input.NewMerchantName))
        {
            var createdMerchant = await _merchantService.GetOrCreateByNameAsync(Input.NewMerchantName.Trim(), cancellationToken: cancellationToken);
            targetMerchantId = createdMerchant.Id;
        }

        var rule = new CategorizationRule
        {
            UserId = string.Empty,
            Name = Input.Name.Trim(),
            Pattern = Input.Pattern.Trim(),
            TargetCategoryId = Input.TargetCategoryId,
            TargetMerchantId = targetMerchantId,
            TargetBankAccountId = Input.TargetBankAccountId,
            Priority = Input.Priority,
            MinAmount = Input.MinAmount,
            MaxAmount = Input.MaxAmount,
            ShiftToNextMonth = Input.ShiftToNextMonth,
            IsActive = true
        };

        _dbContext.CategorizationRules.Add(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Règle '{rule.Name}' créée avec succès.";
        TempData["UpdatedRuleId"] = rule.Id.ToString();
        return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: $"rule-{rule.Id}");
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!Input.Id.HasValue)
        {
            TempData["ErrorMessage"] = "Identifiant de la règle introuvable.";
            return RedirectToPage();
        }

        if (!string.IsNullOrWhiteSpace(Input.Pattern))
        {
            try
            {
                _ = new Regex(Input.Pattern.Trim(), RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
            }
            catch (ArgumentException)
            {
                // La syntaxe invalide de l'expression régulière est capturée pour être restituée sous forme d'erreur de formulaire
                ModelState.AddModelError("Input.Pattern", "Le motif d'expression régulière (Regex) est syntaxiquement invalide.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadViewDataAsync(cancellationToken);
            return Page();
        }

        var rule = await _dbContext.CategorizationRules.FindAsync([Input.Id.Value], cancellationToken);
        if (rule == null)
        {
            TempData["ErrorMessage"] = "Règle introuvable.";
            return RedirectToPage();
        }

        Guid? targetMerchantId = Input.TargetMerchantId;
        if (!targetMerchantId.HasValue && !string.IsNullOrWhiteSpace(Input.NewMerchantName))
        {
            var createdMerchant = await _merchantService.GetOrCreateByNameAsync(Input.NewMerchantName.Trim(), cancellationToken: cancellationToken);
            targetMerchantId = createdMerchant.Id;
        }

        rule.Name = Input.Name.Trim();
        rule.Pattern = Input.Pattern.Trim();
        rule.TargetCategoryId = Input.TargetCategoryId;
        rule.TargetMerchantId = targetMerchantId;
        rule.TargetBankAccountId = Input.TargetBankAccountId;
        rule.Priority = Input.Priority;
        rule.MinAmount = Input.MinAmount;
        rule.MaxAmount = Input.MaxAmount;
        rule.ShiftToNextMonth = Input.ShiftToNextMonth;

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Règle '{rule.Name}' modifiée avec succès.";
        TempData["UpdatedRuleId"] = rule.Id.ToString();
        return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: $"rule-{rule.Id}");
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await _dbContext.CategorizationRules.FindAsync([id], cancellationToken);
        if (rule != null)
        {
            _dbContext.CategorizationRules.Remove(rule);
            await _dbContext.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = $"Règle '{rule.Name}' supprimée avec succès.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApplyRulesAsync(Guid? ruleId = null,
        bool overwriteExisting = false,
        Guid? accountId = null, CancellationToken cancellationToken = default)
    {
        var count = await _rulesEngineService.ApplyRulesToDatabaseAsync(
            overwriteExisting: overwriteExisting,
            bankAccountId: accountId,
            singleRuleId: ruleId,
            cancellationToken: cancellationToken);

        if (count > 0)
        {
            var modeDesc = overwriteExisting ? "en forçant l'écrasement des catégories existantes" : "sur les opérations non catégorisées";
            TempData["SuccessMessage"] = $"{count} opération(s) catégorisée(s) avec succès {modeDesc}.";
        }
        else
        {
            TempData["InfoMessage"] = "Aucune opération mise à jour. Les opérations correspondent déjà aux règles ou aucune nouvelle règle ne s'applique.";
        }

        return RedirectToPage();
    }

    private async Task LoadViewDataAsync(CancellationToken cancellationToken = default)
    {
        Rules = await _dbContext.CategorizationRules
            .Include(r => r.TargetCategory)
            .Include(r => r.TargetMerchant)
            .Include(r => r.TargetBankAccount)
            .OrderBy(r => r.Priority)
            .ToListAsync(cancellationToken);

        AvailableCategories = await _dbContext.Categories
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        AvailableMerchants = await _merchantService.GetAllMerchantsAsync(cancellationToken);

        ParentCategories = await _dbContext.Categories
            .Include(c => c.SubCategories)
            .Where(c => c.ParentCategoryId == null)
            .OrderBy(c => c.IsIncome)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        AvailableAccounts = await _dbContext.BankAccounts
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        CategoryTranslations = await _localizedEntityService.GetTranslationsAsync("Category", AvailableCategories.Select(c => c.Id), "Name", cancellationToken: cancellationToken);
    }
}
