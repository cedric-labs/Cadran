using System.Text.Json;
using SqliteForge.Configuration;

namespace Ledger.Infrastructure.Configuration;

/// <summary>
/// Fournisseur de configuration personnalisé lisant les paramètres métier et système
/// directement depuis une table SQLite dédiée (AppSettings).
/// Délègue son implémentation technique au framework partagé <see cref="SqliteForge.Configuration.SqliteConfigurationProvider"/>.
/// </summary>
/// <param name="connectionStringResolver">Fonction résolvant la chaîne de connexion SQLite active.</param>
public sealed class SqliteConfigurationProvider(Func<string?> connectionStringResolver)
    : SqliteForge.Configuration.SqliteConfigurationProvider(connectionStringResolver, DefaultSettings)
{
    private static readonly SqliteSettingSeed[] DefaultSettings =
    [
        new("App:Name", "Cadran - Gestion Financière", "Nom d'affichage de l'application"),
        new("App:DefaultCurrency", "EUR", "Devise monétaire par défaut du système"),
        new("App:AllowRegistration", "true", "Active ou désactive la création de nouveaux comptes"),
        new("Security:RequireMfa", "false", "Force le MFA TOTP pour l'ensemble des utilisateurs"),
        new("Security:MaxFailedAccessAttempts", "5", "Nombre d'échecs de connexion avant verrouillage"),
        new("Rules:MaxExecutionTimeMs", "500", "Délai maximal alloué par exécution de Regex (anti-ReDoS)"),
        new("Imports:MaxFileSizeBytes", "10485760", "Taille maximale des fichiers d'import (10 Mo)"),
        new("Imports:AutoCategorizeOnImport", "true", "Exécute automatiquement le moteur de règles après import"),
        new("Accounts:DefaultWarningOverdraftThreshold", "0.00", "Seuil d'alerte de découvert par défaut"),
        new("Currencies:Supported", JsonSerializer.Serialize(CurrencyService.DefaultCurrencies), "Liste des devises monétaires configurables du système")
    ];

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="SqliteConfigurationProvider"/> avec une chaîne de connexion statique.
    /// </summary>
    /// <param name="connectionString">Chaîne de connexion SQLite.</param>
    public SqliteConfigurationProvider(string connectionString) : this(() => connectionString)
    {
    }
}
