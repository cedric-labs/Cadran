using Ledger.Features.Security.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ledger.Features.Security.Pages;

public class LogoutModel(IAuthService authService) : PageModel
{
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        await authService.SignOutAsync(cancellationToken);
        return Redirect("/Security/Login");
    }

    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        return OnPostAsync(cancellationToken);
    }
}
