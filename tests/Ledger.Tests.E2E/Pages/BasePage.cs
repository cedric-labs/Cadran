using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Classe de base pour tous les Page Objects de l'application.
/// </summary>
public abstract class BasePage(IPage page)
{
    /// <summary>
    /// Instance Playwright de la page courante.
    /// </summary>
    protected IPage Page { get; } = page ?? throw new ArgumentNullException(nameof(page));
}
