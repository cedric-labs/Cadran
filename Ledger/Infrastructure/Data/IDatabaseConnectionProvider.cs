namespace Ledger.Infrastructure.Data;

/// <summary>
/// Fournisseur dynamique de chaîne de connexion SQLite intégrant la clé de déchiffrement active.
/// Étend le contrat partagé <see cref="SqliteForge.Data.IDatabaseConnectionProvider"/>.
/// </summary>
public interface IDatabaseConnectionProvider : SqliteForge.Data.IDatabaseConnectionProvider;
