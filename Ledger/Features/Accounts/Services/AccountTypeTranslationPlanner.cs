using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Détermine les traductions anglaise, espagnole et allemande manquantes pour les types de comptes par défaut.
/// </summary>
internal static class AccountTypeTranslationPlanner
{
    private static readonly IReadOnlyDictionary<string, (string En, string Es, string De)> DefaultAccountTypeTranslations =
        new Dictionary<string, (string En, string Es, string De)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Checking"] = ("Checking Account", "Cuenta Corriente", "Girokonto"),
            ["CreditCard"] = ("Deferred Debit / Credit Card", "Tarjeta de crédito / débito diferido", "Kreditkarte / Aufgeschobene Debitkarte"),
            ["Cash"] = ("Cash / Wallet", "Efectivo / Cartera", "Bargeld / Geldbörse"),
            ["LivretA"] = ("Livret A Savings", "Cuenta de ahorro Livret A", "Livret A Sparkonto"),
            ["Ldds"] = ("Sustainable Development Savings (LDDS)", "Ahorro Sostenible y Solidario (LDDS)", "Nachhaltiges Sparkonto (LDDS)"),
            ["Lep"] = ("Popular Savings Account (LEP)", "Cuenta de Ahorro Popular (LEP)", "Volkssparkonto (LEP)"),
            ["LivretJeune"] = ("Youth Savings Account", "Cuenta de Ahorro Joven", "Jugendsparkonto"),
            ["Savings"] = ("Bank Savings Account (CSL)", "Cuenta de Ahorro Bancaria (CSL)", "Banksparkonto (CSL)"),
            ["Pel"] = ("Home Savings Plan (PEL)", "Plan de Ahorro para la Vivienda (PEL)", "Bausparplan (PEL)"),
            ["Cel"] = ("Home Savings Account (CEL)", "Cuenta de Ahorro para la Vivienda (CEL)", "Bausparkonto (CEL)"),
            ["Cat"] = ("Term Deposit Account (CAT)", "Depósito a Plazo Fijo", "Festgeldkonto"),
            ["Pea"] = ("Equity Savings Plan (PEA)", "Plan de Ahorro en Acciones (PEA)", "Aktiensparplan (PEA)"),
            ["Investment"] = ("Standard Securities Account (CTO)", "Cuenta de Valores Ordinaria (CTO)", "Wertpapierdepot (CTO)"),
            ["PartsSociales"] = ("Member Shares Account", "Cuenta de Cuotas Participativas", "Genossenschaftsanteile-Konto"),
            ["AssuranceVie"] = ("Life Insurance Policy", "Seguro de Vida", "Lebensversicherung"),
            ["Per"] = ("Retirement Savings Plan (PER)", "Plan de Pensiones / Jubilación (PER)", "Renten-Sparplan (PER)"),
            ["Crypto"] = ("Cryptocurrencies / Other Assets", "Criptomonedas / Otros activos", "Kryptowährungen / Sonstige Vermögenswerte")
        };

    /// <summary>
    /// Retourne les traductions du champ « Name » absentes de <paramref name="existingKeys"/> (clés « idType:langue »).
    /// Les types sans code connu sont ignorés.
    /// </summary>
    public static List<EntityTranslation> PlanMissing(IEnumerable<CustomAccountType> types, ISet<string> existingKeys)
    {
        var missing = new List<EntityTranslation>();
        foreach (var type in types)
        {
            if (string.IsNullOrEmpty(type.Code) || !DefaultAccountTypeTranslations.TryGetValue(type.Code, out var translation))
            {
                continue;
            }

            AddWhenMissing(missing, existingKeys, type.Id, "en", translation.En);
            AddWhenMissing(missing, existingKeys, type.Id, "es", translation.Es);
            AddWhenMissing(missing, existingKeys, type.Id, "de", translation.De);
        }

        return missing;
    }

    private static void AddWhenMissing(List<EntityTranslation> missing, ISet<string> existingKeys, Guid typeId, string languageCode, string value)
    {
        if (existingKeys.Contains($"{typeId}:{languageCode}") || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        missing.Add(new EntityTranslation
        {
            EntityType = "CustomAccountType",
            EntityId = typeId,
            Field = "Name",
            LanguageCode = languageCode,
            Translation = value
        });
    }
}
