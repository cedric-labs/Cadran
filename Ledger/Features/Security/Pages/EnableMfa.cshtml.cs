using System.ComponentModel.DataAnnotations;
using Ledger.Features.Security.Models;
using Ledger.Features.Security.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ledger.Features.Security.Pages;

[Authorize]
public class EnableMfaModel(
    UserManager<ApplicationUser> userManager,
    IAuthService authService) : PageModel
{
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
    public string QrCodeBase64 { get; set; } = string.Empty;
    public bool Is2faEnabled { get; set; }
    public IEnumerable<string>? RecoveryCodes { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Le code de confirmation est obligatoire.")]
        [StringLength(7, ErrorMessage = "Le code doit comporter {1} caractères.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Code à 6 chiffres affiché par l'application")]
        public string Code { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound("Utilisateur introuvable.");
        }

        Is2faEnabled = await userManager.GetTwoFactorEnabledAsync(user);

        var setup = await authService.GenerateMfaSetupAsync(user, cancellationToken);
        SharedKey = setup.SharedKey;
        AuthenticatorUri = setup.AuthenticatorUri;
        QrCodeBase64 = setup.QrCodeBase64;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound("Utilisateur introuvable.");
        }

        if (!ModelState.IsValid)
        {
            var setup = await authService.GenerateMfaSetupAsync(user, cancellationToken);
            SharedKey = setup.SharedKey;
            AuthenticatorUri = setup.AuthenticatorUri;
            QrCodeBase64 = setup.QrCodeBase64;
            return Page();
        }

        var succeeded = await authService.VerifyAndEnableMfaAsync(user, Input.Code, cancellationToken);
        if (!succeeded)
        {
            ModelState.AddModelError("Input.Code", "Code de validation incorrect. Assurez-vous que l'heure de votre téléphone est synchronisée.");
            var setup = await authService.GenerateMfaSetupAsync(user, cancellationToken);
            SharedKey = setup.SharedKey;
            AuthenticatorUri = setup.AuthenticatorUri;
            QrCodeBase64 = setup.QrCodeBase64;
            return Page();
        }

        RecoveryCodes = await authService.GenerateRecoveryCodesAsync(user, cancellationToken);
        Is2faEnabled = true;

        TempData["SuccessMessage"] = "L'authentification à deux facteurs (MFA) a été activée avec succès !";
        return Page();
    }
}
