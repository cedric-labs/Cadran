using System.ComponentModel.DataAnnotations;
using Ledger.Features.Security.Models;
using Ledger.Features.Security.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ledger.Features.Security.Pages;

[AllowAnonymous]
public class LoginWith2faModel(
    SignInManager<ApplicationUser> signInManager,
    IAuthService authService) : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly IAuthService _authService = authService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Le code à 6 chiffres est obligatoire.")]
        [StringLength(7, ErrorMessage = "Le code doit comporter {1} caractères.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Code Authenticator (TOTP)")]
        public string TwoFactorCode { get; set; } = string.Empty;

        [Display(Name = "Mémoriser cet appareil")]
        public bool RememberMachine { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(bool rememberMe, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        // Vérifie qu'une tentative de connexion 2FA est bien en cours
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            return Redirect("/Security/Login");
        }

        RememberMe = rememberMe;
        ReturnUrl = returnUrl;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(bool rememberMe, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        returnUrl ??= Url.Content("~/Accounts");

        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            return Redirect("/Security/Login");
        }

        var result = await _authService.TwoFactorAuthenticatorSignInAsync(
            Input.TwoFactorCode,
            rememberMe,
            Input.RememberMachine,
            cancellationToken);

        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Compte temporairement verrouillé.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Code d'authentification invalide.");
        return Page();
    }
}
