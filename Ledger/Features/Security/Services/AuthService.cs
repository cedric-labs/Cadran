using System.Text.Encodings.Web;
using Ledger.Features.Security.Models;
using Microsoft.AspNetCore.Identity;
using QRCoder;

namespace Ledger.Features.Security.Services;

/// <summary>
/// Service gérant les opérations d'authentification, d'inscription et d'enrôlement MFA TOTP.
/// </summary>
public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    UrlEncoder urlEncoder,
    Ledger.Features.Categories.Services.ICategorySeedService categorySeedService,
    Ledger.Features.Accounts.Services.IAccountTypeService accountTypeService,
    Ledger.Features.Accounts.Services.ITransactionStatusService transactionStatusService) : IAuthService
{
    private const string OtpAuthScheme = "otpauth";

    public async Task<IdentityResult> RegisterUserAsync(string email, string password, string fullName, CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, password).ConfigureAwait(false);
        if (result.Succeeded)
        {
            await categorySeedService.EnsureDefaultCategoriesForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
            await accountTypeService.EnsureDefaultAccountTypesForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
            await transactionStatusService.EnsureDefaultStatusesForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<SignInResult> LoginUserAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user?.UserName is not { } userName)
        {
            return SignInResult.Failed;
        }

        var result = await signInManager.PasswordSignInAsync(
            userName,
            password,
            rememberMe,
            lockoutOnFailure: true).ConfigureAwait(false);

        if (result.Succeeded)
        {
            user.LastLoginAtUtc = DateTime.UtcNow;
            await userManager.UpdateAsync(user).ConfigureAwait(false);
        }

        return result;
    }

    public Task<SignInResult> TwoFactorAuthenticatorSignInAsync(string code, bool rememberMe, bool rememberMachine, CancellationToken cancellationToken = default)
    {
        var cleanCode = code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return signInManager.TwoFactorAuthenticatorSignInAsync(cleanCode, rememberMe, rememberMachine);
    }

    public async Task<MfaSetupDto> GenerateMfaSetupAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        // 1. Récupération ou génération de la clé secrète TOTP
        var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await userManager.ResetAuthenticatorKeyAsync(user).ConfigureAwait(false);
            unformattedKey = await userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        }

        var safeKey = unformattedKey ?? string.Empty;
        var formattedKey = FormatKey(safeKey);
        var email = user.Email ?? user.UserName ?? "utilisateur";
        var encodedIssuer = urlEncoder.Encode("Cadran");
        var encodedEmail = urlEncoder.Encode(email);
        var authenticatorUri = $"{OtpAuthScheme}://totp/{encodedIssuer}:{encodedEmail}?secret={safeKey}&issuer={encodedIssuer}&digits=6";

        // 2. Génération du QR Code via QRCoder (PngByteQRCode compatible Linux sans GDI+)
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(authenticatorUri, QRCodeGenerator.ECCLevel.Q);
        using var pngQrCode = new PngByteQRCode(qrCodeData);
        var qrBytes = pngQrCode.GetGraphic(7);
        var qrCodeBase64 = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";

        return new MfaSetupDto(formattedKey, authenticatorUri, qrCodeBase64);
    }

    public async Task<bool> VerifyAndEnableMfaAsync(ApplicationUser user, string verificationCode, CancellationToken cancellationToken = default)
    {
        var cleanCode = verificationCode.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

        var is2faTokenValid = await userManager.VerifyTwoFactorTokenAsync(
            user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            cleanCode).ConfigureAwait(false);

        if (!is2faTokenValid)
        {
            return false;
        }

        var result = await userManager.SetTwoFactorEnabledAsync(user, true).ConfigureAwait(false);
        return result.Succeeded;
    }

    public Task<IEnumerable<string>?> GenerateRecoveryCodesAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        return userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        return signInManager.SignOutAsync();
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new System.Text.StringBuilder();
        int currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }
        return result.ToString().ToLowerInvariant();
    }
}
