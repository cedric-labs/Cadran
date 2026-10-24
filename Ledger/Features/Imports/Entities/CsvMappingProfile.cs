using Ledger.Domain.Entities;

namespace Ledger.Features.Imports.Entities;

/// <summary>
/// Profil de mapping de colonnes CSV paramétrable par compte bancaire via l'administration.
/// Permet de s'adapter aux exports hétérogènes de toutes les banques (Boursorama, BNP, Crédit Agricole, etc.).
/// </summary>
public class CsvMappingProfile : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Séparateur de colonnes (ex: ";" ou "," ou "\t").
    /// </summary>
    public string Delimiter { get; set; } = ";";

    /// <summary>
    /// Séparateur décimal pour les montants (ex: "," en France, "." en US/UK).
    /// </summary>
    public string DecimalSeparator { get; set; } = ",";

    /// <summary>
    /// Format de date attendu (ex: "dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yy").
    /// </summary>
    public string DateFormat { get; set; } = "dd/MM/yyyy";

    /// <summary>
    /// Indique si la première ligne est un en-tête à ignorer.
    /// </summary>
    public bool HasHeader { get; set; } = true;

    /// <summary>
    /// Index 0-based de la colonne Date.
    /// </summary>
    public int DateColumnIndex { get; set; } = 0;

    /// <summary>
    /// Index 0-based de la colonne Libellé de l'opération.
    /// </summary>
    public int LabelColumnIndex { get; set; } = 1;

    /// <summary>
    /// Index 0-based de la colonne Montant unique (signé ou avec colonne sens).
    /// Si null, utilise Débit/Crédit séparés.
    /// </summary>
    public int? AmountColumnIndex { get; set; } = 2;

    /// <summary>
    /// Index optionnel de la colonne Débit (si les montants sont scindés).
    /// </summary>
    public int? DebitColumnIndex { get; set; }

    /// <summary>
    /// Index optionnel de la colonne Crédit (si les montants sont scindés).
    /// </summary>
    public int? CreditColumnIndex { get; set; }

    /// <summary>
    /// Index optionnel de la référence / note.
    /// </summary>
    public int? ReferenceColumnIndex { get; set; }
}
