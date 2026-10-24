using Ledger.Features.Accounts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ledger.Features.Accounts.Pages;

[Authorize]
public class IndexModel(IAccountBalanceService balanceService) : PageModel
{
    private readonly IAccountBalanceService _balanceService = balanceService;

    public DashboardSummaryDto Dashboard { get; set; } = new(0, 0, 0, 0, new List<AccountBalanceSummaryDto>());

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        Dashboard = await _balanceService.GetDashboardSummaryAsync(cancellationToken);
        return Page();
    }
}
