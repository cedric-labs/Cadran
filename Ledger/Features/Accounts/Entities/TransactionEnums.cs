namespace Ledger.Features.Accounts.Entities;

public enum TransactionType
{
    Debit = 0,    // Dépense / Débit
    Credit = 1,   // Revenu / Crédit
    Transfer = 2  // Virement interne entre comptes
}

public enum TransactionOrigin
{
    Import = 0,   // Issue d'un fichier bancaire importé
    Manual = 1    // Saisie à la main par l'utilisateur
}

public enum TransactionStatus
{
    Pending = 0,     // À venir / Engagée
    Cleared = 1,     // Passée en compte
    Reconciled = 2   // Pointée / Rapprochée
}

public static class TransactionStatusExtensions
{
    public static string ToDisplayName(this TransactionStatus status) => status switch
    {
        TransactionStatus.Pending => "En attente",
        TransactionStatus.Cleared => "Comptabilisée",
        TransactionStatus.Reconciled => "Pointée",
        _ => status.ToString()
    };

    public static string ToBadgeClass(this TransactionStatus status) => status switch
    {
        TransactionStatus.Pending => "bg-warning-subtle text-warning-emphasis border border-warning-subtle",
        TransactionStatus.Cleared => "bg-info-subtle text-info-emphasis border border-info-subtle",
        TransactionStatus.Reconciled => "bg-success-subtle text-success-emphasis border border-success-subtle",
        _ => "bg-secondary-subtle text-secondary"
    };

    public static string ToIconClass(this TransactionStatus status) => status switch
    {
        TransactionStatus.Pending => "bi-hourglass-split",
        TransactionStatus.Cleared => "bi-check-lg",
        TransactionStatus.Reconciled => "bi-check2-all",
        _ => "bi-circle"
    };
}
