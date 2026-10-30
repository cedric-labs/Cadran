using System.ComponentModel.DataAnnotations;
using Ledger.Features.Security.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ledger.Features.Security.Pages;

[AllowAnonymous]
public class LoginModel(IAuthService authService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "L'adresse email est requise.")]
        [EmailAddress(ErrorMessage = "Format d'email invalide.")]
        [Display(Name = "Adresse Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mot de passe")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Se souvenir de moi")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        returnUrl ??= Url.Content("~/Accounts");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await authService.LoginUserAsync(Input.Email, Input.Password, Input.RememberMe, cancellationToken);

        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            return Redirect($"/Security/LoginWith2fa?returnUrl={Uri.EscapeDataString(returnUrl)}&rememberMe={Input.RememberMe}");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Compte temporairement verrouillé suite à plusieurs tentatives infructueuses.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Identifiants invalides. Vérifiez votre email et mot de passe.");
        return Page();
    }
}
