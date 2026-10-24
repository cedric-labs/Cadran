using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Imports.Services;

/// <summary>
/// Représente une opération suspectée de doublon lors de l'import de relevé.
/// </summary>
public record SuspectedDuplicateDto(
    int Index,
    DateTime Date,
    DateTime? ValueDate,
    decimal Amount,
    TransactionType Type,
    string Label,
    string? RawLabel,
    string? ExistingMatchLabel,
    string? Reference = null,
    string? CategoryName = null
);

/// <summary>
/// Groupe d'opérations existantes identifiées comme doublons potentiels.
/// </summary>
public record ExistingDuplicateGroupDto(
    string GroupKey,
    DateTime Date,
    decimal Amount,
    TransactionType Type,
    List<Transaction> Transactions
);

/// <summary>
/// Résultat global d'une tentative d'import de relevé bancaire (OFX/CSV).
/// </summary>
public record ImportResultDto(
    bool Success,
    string Message,
    int TotalParsed,
    int TotalImported,
    int TotalDuplicatesSkipped,
    int TotalAutoCategorized,
    Guid? BatchId,
    IReadOnlyList<SuspectedDuplicateDto>? SuspectedDuplicates = null,
    string? DetectedOfxAcctId = null,
    Guid? TargetBankAccountId = null,
    string? TargetBankAccountName = null,
    bool AutoMatchedByOfx = false,
    bool RequiresAccountSelection = false
);

/// <summary>
/// Contrat du service d'importation et d'analyse des relevés bancaires.
/// </summary>
public interface ITransactionImportService
{
    /// <summary>
    /// Analyse et importe un flux de relevé bancaire vers un compte identifié.
    /// </summary>
    /// <param name="bankAccountId">Identifiant du compte cible optionnel.</param>
    /// <param name="fileName">Nom original du fichier importé.</param>
    /// <param name="fileStream">Flux de données du fichier de relevé.</param>
    /// <param name="csvProfileIdOverride">Profil de parsing CSV spécifique optionnel.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Détail du résultat d'importation.</returns>
    Task<ImportResultDto> ImportStatementAsync(
        Guid? bankAccountId,
        string fileName,
        Stream fileStream,
        Guid? csvProfileIdOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirme et importe les opérations initialement suspectées comme doublons.
    /// </summary>
    /// <param name="batchId">Identifiant du lot d'import.</param>
    /// <param name="bankAccountId">Identifiant du compte bancaire cible.</param>
    /// <param name="confirmedDuplicates">Liste des doublons confirmés par l'utilisateur.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Nombre d'opérations importées.</returns>
    Task<int> ConfirmDuplicatesAsync(
        Guid batchId,
        Guid bankAccountId,
        IReadOnlyList<SuspectedDuplicateDto> confirmedDuplicates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime un lot d'import et l'ensemble des opérations associées.
    /// </summary>
    /// <param name="batchId">Identifiant unique du lot d'import.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Vrai si le lot a été supprimé avec succès.</returns>
    Task<bool> DeleteBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recherche les opérations en doublon déjà enregistrées en base pour un compte donné.
    /// </summary>
    /// <param name="bankAccountId">Identifiant du compte bancaire analysé.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Liste des groupes de doublons détectés.</returns>
    Task<IReadOnlyList<ExistingDuplicateGroupDto>> FindExistingDuplicatesAsync(
        Guid bankAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime définitivement une liste d'opérations en doublon identifiées par leurs identifiants.
    /// </summary>
    /// <param name="bankAccountId">Identifiant du compte bancaire concerné.</param>
    /// <param name="transactionIdsToDelete">Identifiants des opérations à purger.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Nombre d'opérations supprimées.</returns>
    Task<int> DeleteExistingDuplicatesAsync(
        Guid bankAccountId,
        IReadOnlyList<Guid> transactionIdsToDelete,
        CancellationToken cancellationToken = default);
}
