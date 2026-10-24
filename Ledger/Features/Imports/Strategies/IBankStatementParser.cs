using Ledger.Features.Imports.Entities;
using Ledger.Features.Imports.Models;

namespace Ledger.Features.Imports.Strategies;

/// <summary>
/// Contrat du Pattern Strategy pour les différents parsers de formats bancaires (CSV, OFX, QIF...).
/// Permet l'ajout de nouveaux formats d'importation sans impacter le reste du système (Principe Ouvert/Fermé).
/// </summary>
public interface IBankStatementParser
{
    /// <summary>
    /// Nom du format supporté (ex: "CSV", "OFX").
    /// </summary>
    string SupportedFormat { get; }

    /// <summary>
    /// Détermine si la stratégie courante est capable d'interpréter le fichier
    /// en analysant son nom d'extension ou les premiers octets du flux.
    /// </summary>
    bool CanHandle(string fileName, Stream stream);

    /// <summary>
    /// Parse le flux de données en une liste de DTOs d'opérations bancaires.
    /// </summary>
    Task<IReadOnlyList<ImportedTransactionDto>> ParseAsync(
        Stream stream,
        CsvMappingProfile? mappingProfile,
        CancellationToken cancellationToken = default);
}
