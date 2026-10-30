using System.ComponentModel.DataAnnotations;
using Ledger.Features.Security.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ledger.Features.Security.Pages;

[AllowAnonymous]
public class RegisterModel(IAuthService authService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Le nom complet est obligatoire.")]
        [Display(Name = "Nom complet")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "L'adresse email est requise.")]
        [EmailAddress(ErrorMessage = "Format d'email invalide.")]
        [Display(Name = "Adresse Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
        [StringLength(100, ErrorMessage = "Le mot de passe doit comporter au moins {2} caractères.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Mot de passe")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirmer le mot de passe")]
        [Compare("Password", ErrorMessage = "Les deux mots de passe ne correspondent pas.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        returnUrl ??= Url.Content("~/Accounts");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await authService.RegisterUserAsync(Input.Email, Input.Password, Input.FullName, cancellationToken);
        if (result.Succeeded)
        {
            // Auto login after registration
            await authService.LoginUserAsync(Input.Email, Input.Password, rememberMe: false, cancellationToken);
            return LocalRedirect(returnUrl);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }
}
