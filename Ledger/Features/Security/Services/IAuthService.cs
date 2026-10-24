using Ledger.Features.Security.Models;
using Microsoft.AspNetCore.Identity;

namespace Ledger.Features.Security.Services;

/// <summary>
/// Configuration et métadonnées d'enrôlement de l'authentification à deux facteurs (TOTP).
/// </summary>
public record MfaSetupDto(string SharedKey, string AuthenticatorUri, string QrCodeBase64);

/// <summary>
/// Contrat du service d'authentification, d'inscription et de sécurité des accès utilisateur.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Enregistre un nouvel utilisateur et initialise son environnement initial (catégories, statuts, types de comptes).
    /// </summary>
    /// <param name="email">Adresse courriel de connexion.</param>
    /// <param name="password">Mot de passe utilisateur.</param>
    /// <param name="fullName">Nom complet ou pseudonyme.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Résultat de la création de l'identité.</returns>
    Task<IdentityResult> RegisterUserAsync(string email, string password, string fullName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tente la connexion d'un utilisateur par identifiant et mot de passe.
    /// </summary>
    /// <param name="email">Adresse courriel de l'utilisateur.</param>
    /// <param name="password">Mot de passe fourni.</param>
    /// <param name="rememberMe">Indique si le cookie de session doit être persistant.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Résultat de la tentative de connexion.</returns>
    Task<SignInResult> LoginUserAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valide le code TOTP lors d'une connexion nécessitant une double authentification.
    /// </summary>
    /// <param name="code">Code de vérification TOTP à 6 chiffres.</param>
    /// <param name="rememberMe">Persistance de la session.</param>
    /// <param name="rememberMachine">Mémorisation de l'appareil de confiance.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Résultat de la connexion 2FA.</returns>
    Task<SignInResult> TwoFactorAuthenticatorSignInAsync(string code, bool rememberMe, bool rememberMachine, CancellationToken cancellationToken = default);

    /// <summary>
    /// Génère les clés et le QR code pour la configuration d'une application d'authentification TOTP.
    /// </summary>
    /// <param name="user">Utilisateur cible.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Paramètres et image QR Code en base64.</returns>
    Task<MfaSetupDto> GenerateMfaSetupAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valide un code TOTP et active formellement la double authentification pour le compte.
    /// </summary>
    /// <param name="user">Utilisateur cible.</param>
    /// <param name="verificationCode">Code TOTP soumis pour confirmation.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Vrai si le code est valide et la 2FA activée.</returns>
    Task<bool> VerifyAndEnableMfaAsync(ApplicationUser user, string verificationCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Génère un jeu de codes de secours d'urgence pour la double authentification.
    /// </summary>
    /// <param name="user">Utilisateur cible.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Collection des codes de secours générés.</returns>
    Task<IEnumerable<string>?> GenerateRecoveryCodesAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Déconnecte l'utilisateur actuellement authentifié.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task SignOutAsync(CancellationToken cancellationToken = default);
}
