using System.Diagnostics;
using System.Globalization;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Imports.Services;
using Ledger.Features.Imports.Strategies;
using Ledger.Features.Merchants.Services;
using Ledger.Features.Recurring.Services;
using Ledger.Features.Reports.Services;
using Ledger.Features.RulesEngine.Services;
using Ledger.Features.Security.Context;
using Ledger.Features.Security.Models;
using Ledger.Features.Security.Services;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;

SQLitePCL.Batteries_V2.Init();

var builder = WebApplication.CreateBuilder(args);

// ==============================================================================
// 0. CONFIGURATION DU SERVEUR KESTREL (PORT, ACCÈS RÉSEAU)
// ==============================================================================
var serverSection = builder.Configuration.GetSection("Server");
var configuredPort = serverSection.GetValue<int?>("Port");
var allowNetwork = serverSection.GetValue<bool?>("AllowNetworkAccess") ?? false;
var autoLaunchBrowser = serverSection.GetValue<bool?>("AutoLaunchBrowser") ?? false;

if (configuredPort.HasValue)
{
    var scheme = serverSection.GetValue<string>("Scheme") ?? Uri.UriSchemeHttp;
    var host = allowNetwork ? "0.0.0.0" : "localhost";
    builder.WebHost.UseUrls($"{scheme}://{host}:{configuredPort.Value}");
}

// ==============================================================================
// 1. GESTION DU CHIFFREMENT SQLITE (SQLCIPHER) & FOURNISSEUR DE CONNEXION
// ==============================================================================
var baseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=data/cadran.db;Mode=ReadWriteCreate;Foreign Keys=True;";

var keyStore = new LocalProtectedFileKeyStore();
var keyHolder = new DatabaseKeyHolder();
var connectionProvider = new DatabaseConnectionProvider(baseConnectionString, keyHolder);

var dbFilePath = connectionProvider.GetDatabaseFilePath();

// S'assurer que le répertoire de la base de données existe
var dbDirectory = Path.GetDirectoryName(dbFilePath);
if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}

// Rétrocompatibilité : migration automatique si cadran.db existait à la racine
var legacyRootDbPath = Path.Combine(Directory.GetCurrentDirectory(), "cadran.db");
if (File.Exists(legacyRootDbPath) && !File.Exists(dbFilePath) && !string.Equals(Path.GetFullPath(legacyRootDbPath), Path.GetFullPath(dbFilePath), StringComparison.OrdinalIgnoreCase))
{
    File.Move(legacyRootDbPath, dbFilePath);
    var legacyWal = legacyRootDbPath + "-wal";
    var targetWal = dbFilePath + "-wal";
    if (File.Exists(legacyWal))
    {
        File.Move(legacyWal, targetWal);
    }
    var legacyShm = legacyRootDbPath + "-shm";
    var targetShm = dbFilePath + "-shm";
    if (File.Exists(legacyShm))
    {
        File.Move(legacyShm, targetShm);
    }
}

bool isEncrypted = DatabaseEncryptionService.CheckIfFileEncrypted(dbFilePath);
string? activeKey = null;

if (isEncrypted)
{
    var savedKey = await keyStore.GetSavedKeyAsync().ConfigureAwait(false);
    if (!string.IsNullOrEmpty(savedKey) && DatabaseEncryptionService.VerifyPassphrase(baseConnectionString, savedKey))
    {
        activeKey = savedKey;
    }
}

keyHolder.SetKey(activeKey, isEncrypted);

// ==============================================================================
// 2. FOURNISSEUR DE CONFIGURATION DYNAMIQUE SQLITE
// ==============================================================================
// Les paramètres applicatifs sont résolus dynamiquement depuis la base SQLite.
builder.Configuration.AddSqliteConfiguration(() =>
{
    if (keyHolder.IsDatabaseEncrypted && !keyHolder.IsUnlocked)
    {
        return null;
    }
    return connectionProvider.GetConnectionString();
});

// Services d'injection de dépendances pour la sécurité de la base
builder.Services.AddSingleton<IDatabaseKeyHolder>(keyHolder);
builder.Services.AddSingleton<IDatabaseKeyStore>(keyStore);
builder.Services.AddSingleton<IDatabaseConnectionProvider>(connectionProvider);
builder.Services.AddScoped<IDatabaseEncryptionService, DatabaseEncryptionService>();

// ==============================================================================
// 3. PERSISTANCE EF CORE AVEC INTERCEPTEUR WAL & CHIFFREMENT
// ==============================================================================
var walInterceptor = new SqliteWalConnectionInterceptor();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var connProvider = serviceProvider.GetRequiredService<IDatabaseConnectionProvider>();
    options.UseSqlite(connProvider.GetConnectionString());
    options.AddInterceptors(walInterceptor);
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// ==============================================================================
// 4. SÉCURITÉ, ASP.NET CORE IDENTITY & CONTEXTE UTILISATEUR COURANT
// ==============================================================================
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Politique des mots de passe
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;

    // Protection anti-bruteforce (verrouillage dynamique)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = builder.Configuration.GetValue("Security:MaxFailedAccessAttempts", 5);
    options.Lockout.AllowedForNewUsers = true;

    // Unicité de l'email
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Security/Login";
    options.LogoutPath = "/Security/Logout";
    options.AccessDeniedPath = "/Security/Login";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// ==============================================================================
// 5. INJECTION DE DÉPENDANCES PAR TRANCHE VERTICALE (VERTICAL SLICES)
// ==============================================================================
// Module Sécurité
builder.Services.AddScoped<IAuthService, AuthService>();

// Module Comptes, Soldes, Types & Statuts
builder.Services.AddScoped<IAccountBalanceService, AccountBalanceService>();
builder.Services.AddScoped<IAccountTypeService, AccountTypeService>();
builder.Services.AddScoped<ITransactionStatusService, TransactionStatusService>();
builder.Services.AddScoped<ManualTransactionService>();

// Module Catégories & Initialisation par défaut
builder.Services.AddScoped<Ledger.Features.Categories.Services.ICategorySeedService, Ledger.Features.Categories.Services.CategorySeedService>();

// Module Marchands / Tiers
builder.Services.AddScoped<IMerchantService, MerchantService>();

// Module Moteur de Règles (Regex)
builder.Services.AddScoped<IRulesEngineService, RulesEngineService>();

// Module Imports (Pattern Strategy pour CSV et OFX)
builder.Services.AddScoped<IBankStatementParser, CsvStatementParser>();
builder.Services.AddScoped<IBankStatementParser, OfxStatementParser>();
builder.Services.AddScoped<ITransactionImportService, TransactionImportService>();

// Module Charges Récurrentes & Abonnements
builder.Services.AddScoped<IRecurringExpenseService, RecurringExpenseService>();

// Module Rapports & Tableaux de Bord (Chart.js) & TCD (Pivot)
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IPivotAggregationCalculator, PivotAggregationCalculator>();
builder.Services.AddScoped<IPivotReportService, PivotReportService>();

// Module Paramètres UI & Polices
builder.Services.AddScoped<IUiSettingsService, UiSettingsService>();

// Module Localisation & Internationalisation Hybride (Fichiers JSON pour UI + Base pour Entités)
var localesPath = Path.Combine(builder.Environment.ContentRootPath, "Resources", "Locales");
var languageService = new LanguageService(localesPath);
builder.Services.AddSingleton<ILanguageService>(languageService);
builder.Services.AddSingleton<Microsoft.Extensions.Localization.IStringLocalizerFactory>(new JsonStringLocalizerFactory(localesPath));
builder.Services.AddTransient<Microsoft.Extensions.Localization.IStringLocalizer>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizerFactory>().Create("global", ""));
builder.Services.AddScoped<ILocalizedEntityService, LocalizedEntityService>();
builder.Services.AddSingleton<ICurrencyService, CurrencyService>();
builder.Services.AddSingleton(System.Text.Encodings.Web.HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All));
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

// ==============================================================================
// 6. CONFIGURATION DES RAZOR PAGES ET RÉSOLUTIONS DE VUES MODULAIRES
// ==============================================================================
builder.Services.AddRazorPages(options =>
{
    options.RootDirectory = "/";

    // Sécurisation globale des espaces applicatifs
    options.Conventions.AuthorizeFolder("/Accounts");
    options.Conventions.AuthorizeFolder("/Categories");
    options.Conventions.AuthorizeFolder("/Merchants");
    options.Conventions.AuthorizeFolder("/RulesEngine");
    options.Conventions.AuthorizeFolder("/Imports");
    options.Conventions.AuthorizeFolder("/Recurring");
    options.Conventions.AuthorizeFolder("/Reports");
    options.Conventions.AuthorizeFolder("/Settings");
    options.Conventions.AuthorizePage("/Security/EnableMfa");
    options.Conventions.AuthorizePage("/Security/DatabaseEncryption");

    // Accès public
    options.Conventions.AllowAnonymousToPage("/Security/Login");
    options.Conventions.AllowAnonymousToPage("/Security/Register");
    options.Conventions.AllowAnonymousToPage("/Security/LoginWith2fa");
    options.Conventions.AllowAnonymousToPage("/Security/UnlockDatabase");
    options.Conventions.AllowAnonymousToPage("/Index");
});

builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    // Localisation des vues partagées et composants modulaires
    options.PageViewLocationFormats.Add("/Features/{1}/{0}.cshtml");
    options.PageViewLocationFormats.Add("/Features/{1}/Pages/{0}.cshtml");
    options.PageViewLocationFormats.Add("/Features/Categories/Components/{0}.cshtml");
    options.PageViewLocationFormats.Add("/Pages/Shared/{0}.cshtml");
    options.PageViewLocationFormats.Add("/Shared/{0}.cshtml");
});

var app = builder.Build();

// ==============================================================================
// 7. INITIALISATION DU SCHÉMA SQLITE ET MODE WAL AU DÉMARRAGE
// ==============================================================================
if (keyHolder.IsUnlocked)
{
    await DatabaseInitializer.InitializeDatabaseAsync(app.Services).ConfigureAwait(false);
    CurrencyExtensions.Configure(app.Services.GetRequiredService<ICurrencyService>());
}

// ==============================================================================
// 8. PIPELINE HTTP
// ==============================================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Configuration dynamique de la localisation de requêtes (Cookie, QueryString, Accept-Language)
var discoveredLanguages = languageService.GetSupportedLanguages().Select(l => l.Code).ToArray();
if (discoveredLanguages.Length == 0) discoveredLanguages = new[] { "fr", "en" };

static CultureInfo CreateEuroCulture(string cultureCode)
{
    var culture = new CultureInfo(cultureCode);
    culture.NumberFormat.CurrencySymbol = "€";
    return culture;
}

var supportedCultures = discoveredLanguages.Select(CreateEuroCulture).ToList();
var supportedUICultures = discoveredLanguages.Select(CreateEuroCulture).ToList();
var defaultCulture = CreateEuroCulture("fr");

CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(defaultCulture, defaultCulture),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedUICultures
};

app.UseRequestLocalization(localizationOptions);

// Middleware interceptant les requêtes si la base SQLite est verrouillée
app.UseMiddleware<Ledger.Infrastructure.Security.DatabaseLockMiddleware>();

// Endpoint dédié pour changer de langue et rediriger
app.MapGet("/SetLanguage", (string culture, string? returnUrl, HttpContext httpContext, ILanguageService langSvc) =>
{
    var targetCulture = langSvc.IsSupported(culture) ? culture.Trim().ToLowerInvariant() : "fr";

    httpContext.Response.Cookies.Append(
        Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
        Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(
            new Microsoft.AspNetCore.Localization.RequestCulture(targetCulture)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax, HttpOnly = true, Secure = true }
    );

    return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// Endpoint d'arrêt gracieux piloté localement par le superviseur de processus
app.MapPost("/api/system/shutdown", (IHostApplicationLifetime lifetime, HttpContext context) =>
{
    var remoteIp = context.Connection.RemoteIpAddress;
    if (remoteIp is not null && !System.Net.IPAddress.IsLoopback(remoteIp))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    _ = Task.Run(async () =>
    {
        await Task.Delay(200).ConfigureAwait(false);
        lifetime.StopApplication();
    });

    return Results.Ok(new { message = "Arrêt du serveur en cours..." });
}).AllowAnonymous();

if (autoLaunchBrowser)
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try
        {
            var port = configuredPort ?? 5000;
            var targetUrl = $"http://localhost:{port}";

            Process.Start(new ProcessStartInfo
            {
                FileName = targetUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Impossible d'ouvrir automatiquement le navigateur au démarrage.");
        }
    });
}

await app.RunAsync().ConfigureAwait(false);

public partial class Program
{
    protected Program() { }
}
