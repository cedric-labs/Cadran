using System.ComponentModel.DataAnnotations;
using Ledger.Features.Security.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ledger.Features.Security.Pages;

/// <summary>
/// Page de gestion et configuration du chiffrement global SQLite (SQLCipher).
/// Accessible uniquement aux utilisateurs authentifiés.
/// </summary>
[Authorize]
public class DatabaseEncryptionModel(IDatabaseEncryptionService encryptionService) : PageModel
{
    public bool IsEncrypted { get; private set; }
    public bool IsRememberOnDevice { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public EncryptInputModel EncryptInput { get; set; } = new();

    [BindProperty]
    public ChangePassphraseInputModel ChangePassphraseInput { get; set; } = new();

    [BindProperty]
    public DecryptInputModel DecryptInput { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        IsEncrypted = encryptionService.IsDatabaseEncrypted();
        IsRememberOnDevice = await encryptionService.IsRememberOnDeviceEnabledAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostEncryptAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(EncryptInput.NewPassphrase) || EncryptInput.NewPassphrase.Length < 8)
        {
            ErrorMessage = "La phrase de passe doit comporter au moins 8 caractères.";
            return RedirectToPage();
        }

        if (EncryptInput.NewPassphrase != EncryptInput.ConfirmPassphrase)
        {
            ErrorMessage = "La confirmation de la phrase de passe ne correspond pas.";
            return RedirectToPage();
        }

        var (success, error) = await encryptionService.EncryptDatabaseAsync(EncryptInput.NewPassphrase, EncryptInput.RememberOnDevice);
        if (success)
        {
            StatusMessage = "Le chiffrement global de la base de données (SQLCipher AES-256) a été activé avec succès !";
        }
        else
        {
            ErrorMessage = error ?? "Échec de l'activation du chiffrement.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangePassphraseAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ChangePassphraseInput.CurrentPassphrase))
        {
            ErrorMessage = "Veuillez saisir votre phrase de passe actuelle.";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(ChangePassphraseInput.NewPassphrase) || ChangePassphraseInput.NewPassphrase.Length < 8)
        {
            ErrorMessage = "La nouvelle phrase de passe doit comporter au moins 8 caractères.";
            return RedirectToPage();
        }

        if (ChangePassphraseInput.NewPassphrase != ChangePassphraseInput.ConfirmPassphrase)
        {
            ErrorMessage = "La confirmation de la nouvelle phrase de passe ne correspond pas.";
            return RedirectToPage();
        }

        var (success, error) = await encryptionService.ChangePassphraseAsync(
            ChangePassphraseInput.CurrentPassphrase,
            ChangePassphraseInput.NewPassphrase,
            ChangePassphraseInput.RememberOnDevice);

        if (success)
        {
            StatusMessage = "La phrase de passe de chiffrement de la base a été mise à jour avec succès (rekey effectué).";
        }
        else
        {
            ErrorMessage = error ?? "Échec de la modification de la phrase de passe.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleRememberAsync(bool remember, CancellationToken cancellationToken = default)
    {
        await encryptionService.SetRememberOnDeviceAsync(remember);
        StatusMessage = remember
            ? "La mémorisation automatique de la clé sur cet appareil est désormais activée."
            : "La mémorisation sur cet appareil a été révoquée. La phrase de passe sera demandée au prochain lancement.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDecryptAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(DecryptInput.CurrentPassphrase))
        {
            ErrorMessage = "Veuillez saisir votre phrase de passe actuelle pour confirmer le déchiffrement.";
            return RedirectToPage();
        }

        var (success, error) = await encryptionService.DecryptDatabaseToPlainAsync(DecryptInput.CurrentPassphrase);
        if (success)
        {
            StatusMessage = "Le chiffrement de la base a été retiré. Le fichier SQLite est désormais en texte brut non chiffré.";
        }
        else
        {
            ErrorMessage = error ?? "Échec du retrait du chiffrement.";
        }

        return RedirectToPage();
    }

    public class EncryptInputModel
    {
        [Required]
        [DataType(DataType.Password)]
        public string NewPassphrase { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string ConfirmPassphrase { get; set; } = string.Empty;

        public bool RememberOnDevice { get; set; } = true;
    }

    public class ChangePassphraseInputModel
    {
        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassphrase { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string NewPassphrase { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string ConfirmPassphrase { get; set; } = string.Empty;

        public bool RememberOnDevice { get; set; } = true;
    }

    public class DecryptInputModel
    {
        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassphrase { get; set; } = string.Empty;
    }
}
