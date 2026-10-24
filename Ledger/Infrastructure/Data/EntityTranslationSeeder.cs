using System.Globalization;
using System.Text;
using Ledger.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Service assurant le peuplement des traductions multilingues de référence (types de comptes, statuts et catégories).
/// </summary>
public sealed class EntityTranslationSeeder(ILogger<EntityTranslationSeeder> logger)
{
    private static readonly Dictionary<string, string> AccountTypeTranslations = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Checking", "Checking Account" },
        { "CreditCard", "Deferred Debit / Credit Card" },
        { "Cash", "Wallet / Cash" },
        { "LivretA", "Livret A Savings" },
        { "Ldds", "Sustainable Development Savings (LDDS)" },
        { "Lep", "Popular Savings Account (LEP)" },
        { "LivretJeune", "Youth Savings Account" },
        { "Savings", "Bank Savings Account" },
        { "Pel", "Home Savings Plan (PEL)" },
        { "Cel", "Home Savings Account (CEL)" },
        { "Cat", "Term Deposit Account" },
        { "Pea", "Share Savings Plan (PEA)" },
        { "Investment", "Securities Account" },
        { "PartsSociales", "Cooperative Shares" },
        { "AssuranceVie", "Life Insurance" },
        { "Per", "Retirement Savings Plan (PER)" },
        { "Crypto", "Cryptocurrencies & Other Assets" }
    };

    private static readonly Dictionary<string, string> StatusTranslations = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Reconciled", "Reconciled" },
        { "Cleared", "Cleared" },
        { "Pending", "Pending" }
    };

    private static readonly Dictionary<string, string> CategoryTranslationMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Banque, assurances & crédits", "Banking, Insurance & Loans" },
        { "Divers & à catégoriser", "Miscellaneous & Uncategorized" },
        { "Dépenses vie courante", "Daily Living Expenses" },
        { "Enfants, scolarité", "Children & Schooling" },
        { "Impôts, taxes", "Taxes & Duties" },
        { "Logement, immobilier", "Housing & Real Estate" },
        { "Loisirs, sorties & culture", "Leisure, Outings & Culture" },
        { "Revenus & rentrées d'argent", "Income & Money In" },
        { "Santé, prévoyance", "Healthcare & Insurance" },
        { "Services & abonnements pros", "Services & Subscriptions" },
        { "Transports, véhicules", "Transport & Vehicles" },
        { "Virements & épargne", "Transfers & Savings" },
        { "A catégoriser", "To Categorize" },
        { "ADN", "ADN (Anime)" },
        { "Abonnement", "Subscription" },
        { "Abonnements VOD, musique, jeux", "VOD, Music & Gaming Subscriptions" },
        { "Achat véhicule", "Vehicle Purchase" },
        { "Achats, ventes de titres", "Securities Trading" },
        { "Activités enfants", "Children Activities" },
        { "Aide", "Assistance" },
        { "Aides et allocations", "Benefits & Allowances" },
        { "Alimentation, supermarché", "Groceries & Supermarket" },
        { "Amazon Video", "Amazon Prime Video" },
        { "Animaux", "Pets" },
        { "Argent de poche", "Pocket Money" },
        { "Assurance logement", "Home Insurance" },
        { "Assurance prêt immo.", "Mortgage Insurance" },
        { "Assurance scolaire", "School Insurance" },
        { "Assurance véhicule", "Vehicle Insurance" },
        { "Assurances", "Insurance" },
        { "Avances, remboursements", "Advances & Reimbursements" },
        { "Cadeaux offerts", "Gifts Given" },
        { "Cadeaux, dons reçus", "Gifts & Donations Received" },
        { "Carburant", "Fuel" },
        { "Charges logement", "Housing Utilities & Fees" },
        { "Chèques", "Checks" },
        { "Coiffeur, esthétique, cosmétique, soins", "Hairdresser, Cosmetics & Personal Care" },
        { "Conseil financier", "Financial Advisory" },
        { "Conseil juridique", "Legal Advisory" },
        { "Cotis. sports, loisirs", "Sports & Leisure Fees" },
        { "Cours sports, loisirs", "Sports & Leisure Classes" },
        { "Dentiste", "Dentist" },
        { "Diététicienne", "Dietitian" },
        { "Dons caritatifs", "Charity Donations" },
        { "Déblocage prêt, crédits, réserves", "Loan Drawdowns & Credits" },
        { "Eau", "Water Utility" },
        { "Education, formation", "Education & Training" },
        { "Electricité, gaz, chauffage", "Electricity, Gas & Heating" },
        { "Electronique, multimédia", "Electronics & Media" },
        { "Enfants", "Children" },
        { "Entretien adoucisseur", "Water Softener Maintenance" },
        { "Entretien chaudière", "Boiler Maintenance" },
        { "Entretien poêle à granulés", "Pellet Stove Maintenance" },
        { "Entretien, équipements véhicules", "Vehicle Maintenance & Gear" },
        { "Equipement sports, loisirs", "Sports & Leisure Equipment" },
        { "FDJ", "Lottery & Gaming (FDJ)" },
        { "Frais bancaires", "Bank Fees" },
        { "Frais, remb santé", "Medical Expenses & Reimbursements" },
        { "GamePass", "Xbox Game Pass" },
        { "Garage, parking privé", "Garage & Private Parking" },
        { "Garde d'enfants", "Childcare" },
        { "Github", "GitHub" },
        { "Google cloud", "Google Cloud" },
        { "Habillement, accessoires", "Clothing & Accessories" },
        { "Hors budget", "Out of Budget" },
        { "Hôtel", "Hotel & Lodging" },
        { "Impôts", "Income Taxes" },
        { "Inscription, scolarité", "Tuition & School Registration" },
        { "Internet, TV, télécom", "Internet, TV & Telecom" },
        { "Jeux vidéo", "Video Games" },
        { "Jouets, cadeaux", "Toys & Presents" },
        { "Linxo", "Linxo" },
        { "Livres, fournitures (éducation)", "Books & School Supplies" },
        { "Loyer", "Rent" },
        { "Machine café Yzee-Services", "Coffee Machine (Yzee-Services)" },
        { "Masques chirurgicaux", "Surgical Masks & Health Supplies" },
        { "Medecin", "Doctor / Physician" },
        { "Meubles, équipement", "Furniture & Home Equipment" },
        { "Musique, livres, films", "Music, Books & Movies" },
        { "NETFLIX", "Netflix" },
        { "Notes de frais (+/-)", "Expense Reports (+/-)" },
        { "Office 365", "Microsoft 365" },
        { "Opticien", "Optician / Eyewear" },
        { "Orthodontiste", "Orthodontist" },
        { "Parking, péages, PV", "Parking, Tolls & Fines" },
        { "Passion, hobby", "Hobbies & Passions" },
        { "Permis de conduire", "Driving License" },
        { "Poste, courrier", "Post Office & Mail" },
        { "Remb. prêt conso", "Consumer Loan Repayment" },
        { "Remb. prêt immobilier", "Mortgage Repayment" },
        { "Remboursement", "Reimbursement" },
        { "Restaurants, soirées", "Restaurants & Nightlife" },
        { "Restauration scolaire", "School Cafeteria" },
        { "Revenus", "Income" },
        { "Revenus de remplacement", "Replacement Income / Benefits" },
        { "Salaire/Revenus d'activité", "Salary & Professional Income" },
        { "Services", "Services" },
        { "Shopping/e-Commerce", "Shopping & eCommerce" },
        { "Snacks, repas au travail", "Snacks & Work Lunches" },
        { "Sorties culturelles", "Cultural Outings" },
        { "Sorties détente", "Relaxation & Wellness" },
        { "Sorties, voyages", "Outings & Trips" },
        { "Sports, loisirs", "Sports & Leisure" },
        { "Spotify", "Spotify" },
        { "Stockage meubles", "Storage Unit" },
        { "Tabac/Presse", "Tobacco & Newsagent" },
        { "Transports en commun", "Public Transit" },
        { "Travaux, déco, jardin", "Home Improvement, Decor & Gardening" },
        { "Vente immobiliére", "Real Estate Sale" },
        { "Virements d'épargne", "Savings Transfers" },
        { "Virements internes", "Internal Transfers" },
        { "Virements à catégoriser", "Transfers to Categorize" },
        { "Voyages", "Travel & Holidays" },
        { "Vétérinaire", "Veterinarian" },
        { "Warcraft", "World of Warcraft" }
    };

    /// <summary>
    /// Initialise les traductions par défaut pour les types de compte, statuts d'opérations et catégories.
    /// </summary>
    public async Task SeedDefaultTranslationsAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var existingTranslations = await context.EntityTranslations.ToListAsync(cancellationToken);
            var existingKeys = existingTranslations
                .Select(t => $"{t.EntityType.ToLowerInvariant()}:{t.EntityId}:{t.LanguageCode.ToLowerInvariant()}:{t.Field.ToLowerInvariant()}")
                .ToHashSet();

            await SeedAccountTypeTranslationsAsync(context, existingKeys, cancellationToken);
            await SeedStatusTranslationsAsync(context, existingKeys, cancellationToken);
            await SeedCategoryTranslationsAsync(context, existingKeys, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Avertissement lors de l'initialisation des traductions par défaut.");
        }
    }

    private static async Task SeedAccountTypeTranslationsAsync(AppDbContext context, HashSet<string> existingKeys, CancellationToken cancellationToken)
    {
        var accountTypes = await context.CustomAccountTypes.IgnoreQueryFilters().ToListAsync(cancellationToken);
        foreach (var type in accountTypes)
        {
            if (!string.IsNullOrEmpty(type.Code) && AccountTypeTranslations.TryGetValue(type.Code, out var enName))
            {
                var key = $"customaccounttype:{type.Id}:en:name";
                if (!existingKeys.Contains(key))
                {
                    context.EntityTranslations.Add(new EntityTranslation
                    {
                        EntityType = "CustomAccountType",
                        EntityId = type.Id,
                        LanguageCode = "en",
                        Field = "Name",
                        Translation = enName
                    });
                    existingKeys.Add(key);
                }
            }
        }
    }

    private static async Task SeedStatusTranslationsAsync(AppDbContext context, HashSet<string> existingKeys, CancellationToken cancellationToken)
    {
        var statuses = await context.TransactionStatuses.IgnoreQueryFilters().ToListAsync(cancellationToken);
        foreach (var status in statuses)
        {
            if (!string.IsNullOrEmpty(status.Code) && StatusTranslations.TryGetValue(status.Code, out var enName))
            {
                var key = $"transactioncustomstatus:{status.Id}:en:name";
                if (!existingKeys.Contains(key))
                {
                    context.EntityTranslations.Add(new EntityTranslation
                    {
                        EntityType = "TransactionCustomStatus",
                        EntityId = status.Id,
                        LanguageCode = "en",
                        Field = "Name",
                        Translation = enName
                    });
                    existingKeys.Add(key);
                }
            }
        }
    }

    private static async Task SeedCategoryTranslationsAsync(AppDbContext context, HashSet<string> existingKeys, CancellationToken cancellationToken)
    {
        var categories = await context.Categories.IgnoreQueryFilters().ToListAsync(cancellationToken);
        foreach (var category in categories)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                continue;
            }

            var matchedTranslation = ResolveCategoryTranslation(category.Name);
            if (!string.IsNullOrEmpty(matchedTranslation))
            {
                var key = $"category:{category.Id}:en:name";
                if (!existingKeys.Contains(key))
                {
                    context.EntityTranslations.Add(new EntityTranslation
                    {
                        EntityType = "Category",
                        EntityId = category.Id,
                        LanguageCode = "en",
                        Field = "Name",
                        Translation = matchedTranslation
                    });
                    existingKeys.Add(key);
                }
            }
        }
    }

    private static string? ResolveCategoryTranslation(string categoryName)
    {
        if (CategoryTranslationMap.TryGetValue(categoryName, out var tr))
        {
            return tr;
        }

        var normalizedTarget = StripAccents(categoryName).ToLowerInvariant();
        var matchedKvp = CategoryTranslationMap.FirstOrDefault(kvp =>
            string.Equals(StripAccents(kvp.Key).ToLowerInvariant(), normalizedTarget, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrEmpty(matchedKvp.Value) ? null : matchedKvp.Value;
    }

    private static string StripAccents(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(text.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}
