using Ledger.Features.Accounts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ledger.Features.Accounts.Pages;

/// <summary>
/// Liste les opérations saisies à la main qui ressemblent à une opération importée et permet de les fusionner.
/// </summary>
[Authorize]
public class ReconcileModel(ManualEntryMatchLoader matchLoader, ManualEntryMerger merger) : PageModel
{
    public IReadOnlyList<ManualEntryMatch> Matches { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        Matches = await matchLoader.LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostMergeAsync(Guid manualId, Guid importedId, CancellationToken cancellationToken = default)
    {
        var result = await merger.MergeAsync(manualId, importedId, cancellationToken);

        // Clé et non texte : la vue localise le message dans la langue de l'utilisateur.
        TempData["ReconcileResult"] = result.ToString();
        return RedirectToPage();
    }
}
