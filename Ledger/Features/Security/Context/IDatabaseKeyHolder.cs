namespace Ledger.Features.Security.Context;

/// <summary>
/// Contrat du détenteur en mémoire de la clé de déchiffrement de la base SQLite.
/// Étend le contrat partagé <see cref="SqliteForge.Security.IDatabaseKeyHolder"/>.
/// </summary>
public interface IDatabaseKeyHolder : SqliteForge.Security.IDatabaseKeyHolder;
