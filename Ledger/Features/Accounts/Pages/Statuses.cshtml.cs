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
public class StatusesModel(
    ITransactionStatusService statusService,
    AppDbContext dbContext,
    Ledger.Infrastructure.Configuration.ILocalizedEntityService localizedEntityService) : PageModel
{
    private readonly ITransactionStatusService _statusService = statusService;
    private readonly AppDbContext _dbContext = dbContext;
    private readonly Ledger.Infrastructure.Configuration.ILocalizedEntityService _localizedEntityService = localizedEntityService;

    public List<StatusItemViewModel> Statuses { get; set; } = new();

    public class StatusItemViewModel
    {
        public TransactionCustomStatus Status { get; set; } = new() { Name = string.Empty, Code = string.Empty, UserId = string.Empty };
        public string LocalizedName { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
    }

    [BindProperty]
    public StatusInputModel Input { get; set; } = new();

    public class StatusInputModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Le nom du statut est obligatoire.")]
        [StringLength(80, ErrorMessage = "Le nom ne doit pas dépasser 80 caractères.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(250, ErrorMessage = "La description ne doit pas dépasser 250 caractères.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "La couleur est obligatoire.")]
        [RegularExpression("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$", ErrorMessage = "Format hexadécimal invalide (ex: #3b82f6).")]
        public string ColorHex { get; set; } = "#3b82f6";

        [Required(ErrorMessage = "L'icône est obligatoire.")]
        public string IconName { get; set; } = "bi-check-circle";

        public bool IsDefault { get; set; } = false;
    }

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var allStatuses = await _statusService.GetAllStatusesAsync(cancellationToken);

        // Récupérer le nombre de transactions associées à chaque statut
        var counts = await _dbContext.Transactions
            .Where(t => t.CustomStatusId != null)
            .GroupBy(t => t.CustomStatusId)
            .Select(g => new { StatusId = g.Key ?? Guid.Empty, Count = g.Count() })
            .ToDictionaryAsync(g => g.StatusId, g => g.Count, cancellationToken);

        var currentLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        var translations = await _localizedEntityService.GetTranslationsAsync(
            "TransactionCustomStatus",
            allStatuses.Select(s => s.Id),
            "Name",
            currentLang,
            cancellationToken);

        Statuses = allStatuses.Select(s => new StatusItemViewModel
        {
            Status = s,
            LocalizedName = translations.TryGetValue(s.Id, out var val) && !string.IsNullOrWhiteSpace(val) ? val : s.Name,
            TransactionCount = counts.TryGetValue(s.Id, out var count) ? count : 0
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
            var existing = await _statusService.GetByIdAsync(Input.Id.Value, cancellationToken);
            if (existing == null)
            {
                TempData["ErrorMessage"] = "Statut introuvable.";
                return RedirectToPage();
            }

            if (currentLang == "fr")
            {
                existing.Name = Input.Name.Trim();
            }
            existing.Description = Input.Description?.Trim();
            existing.ColorHex = Input.ColorHex.Trim();
            existing.IconName = Input.IconName.Trim();
            existing.IsDefault = Input.IsDefault;

            await _statusService.UpdateAsync(existing, cancellationToken);
            targetId = existing.Id;
            TempData["SuccessMessage"] = $"Le statut '{Input.Name.Trim()}' a été mis à jour avec succès.";
        }
        else
        {
            var newStatus = new TransactionCustomStatus
            {
                UserId = string.Empty, // géré par le service
                Name = Input.Name.Trim(),
                Description = Input.Description?.Trim(),
                ColorHex = Input.ColorHex.Trim(),
                IconName = Input.IconName.Trim(),
                IsDefault = Input.IsDefault,
                IsSystem = false
            };

            await _statusService.CreateAsync(newStatus, cancellationToken);
            targetId = newStatus.Id;
            TempData["SuccessMessage"] = $"Le statut '{newStatus.Name}' a été créé avec succès.";
        }

        // Sauvegarder la traduction pour la langue active
        await _localizedEntityService.SetTranslationAsync(
            "TransactionCustomStatus",
            targetId,
            "Name",
            currentLang,
            Input.Name.Trim(),
            cancellationToken);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetDefaultAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var status = await _statusService.GetByIdAsync(id, cancellationToken);
        if (status == null)
        {
            TempData["ErrorMessage"] = "Statut introuvable.";
            return RedirectToPage();
        }

        status.IsDefault = true;
        await _statusService.UpdateAsync(status, cancellationToken);
        TempData["SuccessMessage"] = $"Le statut '{status.Name}' est désormais le statut par défaut.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (success, errorMessage) = await _statusService.DeleteAsync(id, cancellationToken);
        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage ?? "Impossible de supprimer ce statut.";
        }
        else
        {
            TempData["SuccessMessage"] = "Le statut a été supprimé avec succès.";
        }

        return RedirectToPage();
    }
}
