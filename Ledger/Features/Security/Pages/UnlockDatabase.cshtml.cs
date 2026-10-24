using System.ComponentModel.DataAnnotations;
using Ledger.Features.Security.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ledger.Features.Security.Pages;

/// <summary>
/// Page de déverrouillage de la base de données chiffrée par SQLCipher.
/// </summary>
[AllowAnonymous]
public class UnlockDatabaseModel(IDatabaseEncryptionService encryptionService) : PageModel
{
    private readonly IDatabaseEncryptionService _encryptionService = encryptionService;

    [BindProperty]
    [Required(ErrorMessage = "Veuillez saisir la phrase de passe de déchiffrement.")]
    [DataType(DataType.Password)]
    public string Passphrase { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberOnDevice { get; set; } = true;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (!_encryptionService.IsDatabaseLocked())
        {
            return LocalRedirect("~/Accounts");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Passphrase))
        {
            ErrorMessage = "Veuillez saisir la phrase de passe de déchiffrement.";
            return Page();
        }

        var success = await _encryptionService.UnlockDatabaseAsync(Passphrase, RememberOnDevice);
        if (!success)
        {
            ErrorMessage = "Phrase de passe incorrecte. Impossible de déchiffrer la base de données.";
            return Page();
        }

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) && !ReturnUrl.StartsWith("/Security/UnlockDatabase", StringComparison.OrdinalIgnoreCase))
        {
            return LocalRedirect(ReturnUrl);
        }

        return LocalRedirect("~/Accounts");
    }
}
