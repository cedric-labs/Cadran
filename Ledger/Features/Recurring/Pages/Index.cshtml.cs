using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Models;
using Ledger.Features.Recurring.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Ledger.Features.Recurring.Pages;

[Authorize]
public class IndexModel(
    IRecurringExpenseService recurringService,
    AppDbContext dbContext,
    ILocalizedEntityService localizedEntityService,
    IStringLocalizer localizer) : PageModel
{
    public MonthlyRecurringSummaryDto Summary { get; set; } = new();
    public List<RecurringExpense> AllExpenses { get; set; } = new();
    public List<RecurringCandidateDto> Candidates { get; set; } = new();
    public List<Category> AvailableCategories { get; set; } = new();
    public List<BankAccount> AvailableAccounts { get; set; } = new();
    public Dictionary<Guid, string> CategoryTranslations { get; set; } = new();

    public int SelectedYear { get; set; }
    public int SelectedMonth { get; set; }

    [BindProperty]
    public RecurringExpenseInput Input { get; set; } = new();

    public class RecurringExpenseInput
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Common.Required")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Common.Required")]
        public string Pattern { get; set; } = string.Empty;

        [Required(ErrorMessage = "Common.Required")]
        [Range(0.01, 1000000.0, ErrorMessage = "Common.InvalidAmount")]
        public decimal ExpectedAmount { get; set; }

        public decimal AmountTolerancePercentage { get; set; } = 0.05m;

        public RecurringFrequency Frequency { get; set; } = RecurringFrequency.Monthly;

        [Range(1, 31, ErrorMessage = "Common.InvalidDay")]
        public int? ExpectedDayOfMonth { get; set; }

        public Guid? TargetCategoryId { get; set; }

        public Guid? TargetBankAccountId { get; set; }

        public bool IsActive { get; set; } = true;

        public string? Notes { get; set; }
    }

    public Task OnGetAsync(int? year, int? month, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Today;
        SelectedYear = year ?? now.Year;
        SelectedMonth = month ?? now.Month;

        return LoadDataAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSaveAsync(int? year, int? month, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Today;
        SelectedYear = year ?? now.Year;
        SelectedMonth = month ?? now.Month;

        if (!ModelState.IsValid)
        {
            await LoadDataAsync(cancellationToken);
            return Page();
        }

        if (Input.Id.HasValue && Input.Id.Value != Guid.Empty)
        {
            var existing = await recurringService.GetByIdAsync(Input.Id.Value, cancellationToken);
            if (existing != null)
            {
                existing.Name = Input.Name.Trim();
                existing.Pattern = Input.Pattern.Trim();
                existing.ExpectedAmount = Input.ExpectedAmount;
                existing.AmountTolerancePercentage = Input.AmountTolerancePercentage;
                existing.Frequency = Input.Frequency;
                existing.ExpectedDayOfMonth = Input.ExpectedDayOfMonth;
                existing.TargetCategoryId = Input.TargetCategoryId;
                existing.TargetBankAccountId = Input.TargetBankAccountId;
                existing.IsActive = Input.IsActive;
                existing.Notes = Input.Notes;

                await recurringService.UpdateAsync(existing, cancellationToken);
                TempData["SuccessMessage"] = localizer["Recurring.UpdatedSuccess"].Value;
            }
        }
        else
        {
            var newExpense = new RecurringExpense
            {
                UserId = string.Empty,
                Name = Input.Name.Trim(),
                Pattern = Input.Pattern.Trim(),
                ExpectedAmount = Input.ExpectedAmount,
                AmountTolerancePercentage = Input.AmountTolerancePercentage,
                Frequency = Input.Frequency,
                ExpectedDayOfMonth = Input.ExpectedDayOfMonth,
                TargetCategoryId = Input.TargetCategoryId,
                TargetBankAccountId = Input.TargetBankAccountId,
                IsActive = Input.IsActive,
                AutoDetected = false,
                Notes = Input.Notes
            };

            await recurringService.CreateAsync(newExpense, cancellationToken);
            TempData["SuccessMessage"] = localizer["Recurring.CreatedSuccess"].Value;
        }

        return RedirectToPage(new { year = SelectedYear, month = SelectedMonth });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, int? year, int? month, CancellationToken cancellationToken = default)
    {
        await recurringService.DeleteAsync(id, cancellationToken);
        TempData["SuccessMessage"] = localizer["Recurring.DeletedSuccess"].Value;
        return RedirectToPage(new { year = year ?? DateTime.Today.Year, month = month ?? DateTime.Today.Month });
    }

    public async Task<IActionResult> OnPostConfirmCandidateAsync(string pattern,
        string suggestedName,
        decimal amount,
        int? day,
        int frequency,
        Guid? categoryId,
        Guid? bankAccountId,
        int? year,
        int? month, CancellationToken cancellationToken = default)
    {
        var candidate = new RecurringCandidateDto
        {
            Pattern = pattern,
            SuggestedName = suggestedName,
            AverageAmount = amount,
            LastAmount = amount,
            ExpectedDayOfMonth = day,
            Frequency = (RecurringFrequency)frequency,
            SuggestedCategoryId = categoryId,
            BankAccountId = bankAccountId
        };

        await recurringService.ConfirmCandidateAsync(candidate, cancellationToken);
        TempData["SuccessMessage"] = string.Format(CultureInfo.CurrentCulture, localizer["Recurring.CandidateConfirmed"].Value, suggestedName);

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            return new JsonResult(new { success = true, message = string.Format(CultureInfo.CurrentCulture, localizer["Recurring.CandidateConfirmed"].Value, suggestedName) });
        }

        return RedirectToPage(new { year = year ?? DateTime.Today.Year, month = month ?? DateTime.Today.Month });
    }

    public async Task<IActionResult> OnPostIgnoreCandidateAsync(string pattern, int? year, int? month, CancellationToken cancellationToken = default)
    {
        await recurringService.IgnoreCandidateAsync(pattern, cancellationToken);
        TempData["SuccessMessage"] = string.Format(CultureInfo.CurrentCulture, localizer["Recurring.CandidateIgnored"].Value, pattern);

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            return new JsonResult(new { success = true, message = string.Format(CultureInfo.CurrentCulture, localizer["Recurring.CandidateIgnored"].Value, pattern) });
        }

        return RedirectToPage(new { year = year ?? DateTime.Today.Year, month = month ?? DateTime.Today.Month });
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        Summary = await recurringService.GetMonthlySummaryAsync(SelectedYear, SelectedMonth, cancellationToken);
        AllExpenses = await recurringService.GetAllAsync(cancellationToken);
        Candidates = await recurringService.DetectCandidatesAsync(cancellationToken);

        AvailableCategories = await dbContext.Categories
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var catIds = AvailableCategories.Select(c => c.Id).ToList();
        CategoryTranslations = await localizedEntityService.GetTranslationsAsync("Category", catIds, "Name", cancellationToken: cancellationToken);

        AvailableAccounts = await dbContext.BankAccounts
            .Where(a => !a.IsArchived)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }
}
