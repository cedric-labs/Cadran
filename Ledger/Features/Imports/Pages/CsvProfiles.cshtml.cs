using System.ComponentModel.DataAnnotations;
using Ledger.Features.Imports.Entities;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Ledger.Features.Imports.Pages;

[Authorize]
public class CsvProfilesModel(AppDbContext dbContext, IStringLocalizer localizer) : PageModel
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IStringLocalizer _localizer = localizer;

    public List<CsvMappingProfile> Profiles { get; set; } = new();

    [BindProperty]
    public ProfileInput Input { get; set; } = new();

    public class ProfileInput
    {
        [Required(ErrorMessage = "Le nom du profil est requis.")]
        [Display(Name = "Nom du profil (ex: BoursoBank CSV standard)")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Séparateur de champs")]
        public string Delimiter { get; set; } = ";";

        [Required]
        [Display(Name = "Séparateur décimal")]
        public string DecimalSeparator { get; set; } = ",";

        [Required]
        [Display(Name = "Format de date")]
        public string DateFormat { get; set; } = "dd/MM/yyyy";

        [Display(Name = "La première ligne est un en-tête")]
        public bool HasHeader { get; set; } = true;

        [Required]
        [Display(Name = "Index colonne Date (0-based)")]
        public int DateColumnIndex { get; set; } = 0;

        [Required]
        [Display(Name = "Index colonne Libellé (0-based)")]
        public int LabelColumnIndex { get; set; } = 1;

        [Display(Name = "Index colonne Montant (si unique)")]
        public int? AmountColumnIndex { get; set; } = 2;

        [Display(Name = "Index colonne Débit (si colonnes séparées)")]
        public int? DebitColumnIndex { get; set; }

        [Display(Name = "Index colonne Crédit (si colonnes séparées)")]
        public int? CreditColumnIndex { get; set; }

        [Display(Name = "Index colonne Référence / ID")]
        public int? ReferenceColumnIndex { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        Profiles = await _dbContext.CsvMappingProfiles.OrderBy(p => p.Name).ToListAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            Profiles = await _dbContext.CsvMappingProfiles.OrderBy(p => p.Name).ToListAsync(cancellationToken);
            return Page();
        }

        var profile = new CsvMappingProfile
        {
            UserId = string.Empty,
            Name = Input.Name.Trim(),
            Delimiter = Input.Delimiter,
            DecimalSeparator = Input.DecimalSeparator,
            DateFormat = Input.DateFormat.Trim(),
            HasHeader = Input.HasHeader,
            DateColumnIndex = Input.DateColumnIndex,
            LabelColumnIndex = Input.LabelColumnIndex,
            AmountColumnIndex = Input.AmountColumnIndex,
            DebitColumnIndex = Input.DebitColumnIndex,
            CreditColumnIndex = Input.CreditColumnIndex,
            ReferenceColumnIndex = Input.ReferenceColumnIndex
        };

        _dbContext.CsvMappingProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = _localizer["CsvProfiles.ProfileSavedSuccess"].Value;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.CsvMappingProfiles.FindAsync([id], cancellationToken);
        if (profile != null)
        {
            _dbContext.CsvMappingProfiles.Remove(profile);
            await _dbContext.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = _localizer["CsvProfiles.ProfileDeletedSuccess"].Value;
        }
        return RedirectToPage();
    }
}
