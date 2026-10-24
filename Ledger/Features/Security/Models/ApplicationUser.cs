using Microsoft.AspNetCore.Identity;

namespace Ledger.Features.Security.Models;

/// <summary>
/// Utilisateur applicatif personnalisé étendant IdentityUser.
/// Supporte l'authentification standard, le hachage sécurisé et le MFA TOTP.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
}
