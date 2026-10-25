# Cadran - Gestion de Finances Personnelles & de Patrimoine

<p align="center">
  <strong>Langues :</strong>
  <a href="README.md">English</a> |
  <strong>Français</strong> |
  <a href="README.es.md">Español</a> |
  <a href="README.de.md">Deutsch</a>
</p>

<p align="center">
  <img src="assets/cadran-logo.png" alt="Logo Cadran" width="220" />
  <br/>
  <a href="https://ko-fi.com/curvecoder"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Soutenir curvecoder sur Ko-fi" /></a>
</p>

<p align="center">
  <strong>Une plateforme moderne de gestion financière personnelle, de suivi budgétaire et de patrimoine, locale et respectueuse de la vie privée.</strong>
  <br/>
  Propulsée par ASP.NET Core 9, C# 12, Entity Framework Core et SQLite chiffré (SQLCipher).
</p>

---

## Documentation

- 🗺️ **[Guide Visuel Écran par Écran (docs/fr/README.md)](docs/fr/README.md)** : Manuel illustré interactif avec captures d'écran haute résolution, repères d'action numérotés et fiches pratiques pour les 24 écrans des 6 lots fonctionnels (Sécurité, Comptes, Transactions, Règles & Budgets, Imports et Rapports).
- 📖 **[Guide Utilisateur & Exploitation (docs/fr/user-guide.md)](docs/fr/user-guide.md)** : Manuel exhaustif couvrant la prise en main rapide, les tutoriels pas à pas, le superviseur Windows Tray, le service Windows (NSSM), le binaire autonome Linux, l'archive de déploiement Linux (`.tar.gz`), l'intégration `systemd`, les déploiements hors-ligne (air-gapped) et les concepts financiers.
- 🏗️ **[Spécification d'Architecture & d'Ingénierie (docs/fr/architecture.md)](docs/fr/architecture.md)** : Référence technique approfondie détaillant l'architecture logicielle, les frameworks partagés découplés (`StatementForge`, `SqliteForge`, `LocaleForge`, `FileForge`), les modèles de concurrence/mémoire et la sécurité à deux niveaux.

---

## Pourquoi Cadran ?

La plupart des applications de finances personnelles nécessitent de communiquer vos identifiants bancaires à des agrégateurs tiers dans le cloud, exposant vos transactions confidentielles à des intermédiaires, ou imposent des abonnements logiciels hébergés susceptibles de disparaître ou de subir des fuites de données.

**Cadran repose sur des principes fondamentaux opposés :**
1. **Souveraineté des données (Local-First)** : Toutes vos transactions, comptes bancaires, catégories et règles sont stockés localement dans une unique base de données SQLite (`cadran.db`). Aucune donnée financière ne quitte votre machine.
2. **Zéro scraper distant ni agrégateur tiers** : Gardez le contrôle absolu sur vos relevés bancaires en important directement les fichiers **OFX**, **QFX** ou **CSV** fournis par vos banques.
3. **Isolation multi-utilisateurs** : Hébergez plusieurs espaces distincts sur une même instance (membres d'un foyer, gestion professionnelle vs personnelle) avec des registres, règles et tableaux de bord cloisonnés.
4. **Sécurité à deux niveaux** : Protection par hachage de mot de passe au niveau compte combinée à un chiffrement matériellement indépendant au repos (**AES-256 / SQLCipher**). Pour un accès distant, l'utilisation d'un VPN privé (type WireGuard) est recommandée.
5. **100 % hors-ligne & autonome (Air-Gapped)** : Tous les actifs web, icônes, polices et scripts sont embarqués localement. Aucun CDN, pixel de traçage ou connexion internet n'est requis.

---

## Fonctionnalités Clés

| Domaine | Capacités |
|---|---|
| **Patrimoine & Comptes** | Vue consolidée du patrimoine net (Liquidités, Épargne & Investissements, Dettes / Découverts), 16 types de comptes préconfigurés et statuts personnalisables, support multi-devises (EUR, USD, GBP, CHF, CAD, etc.). |
| **Import & Dédoublonnage de Relevés** | Ingestion optimisée des fichiers **OFX / QFX** (association automatique par `<ACCTID>`) et **CSV** personnalisables (profils de mappage réutilisables). Prévention stricte des doublons et réconciliation interactive par recherche floue. |
| **Livre de Comptes & Ventilations** | Recherche multi-critères instantanée, ventilation des transactions sur plusieurs catégories et tiers, décalage budgétaire (imputation des salaires de fin de mois sur M+1) et rapprochement par lot. |
| **Catégories & Tiers** | Arborescence à deux niveaux (catégories parentes et sous-catégories), classification Dépense / Revenu / Salaire, bilinguisme natif (anglais / français) et normalisation des tiers/commerçants. |
| **Moteur de Règles Automatiques** | Règles de catégorisation hiérarchisées avec jokers (`*`, `%`, `?`) ou expressions régulières, filtres par compte et montant, assignation automatique des tiers et rétroactivité. |
| **Dépenses Récurrentes & Reste à Vivre** | Détection automatique des abonnements et factures périodiques avec score de confiance, suivi d'état (Débité, En attente, En retard) et prévision dynamique du **Reste à vivre** (*Safe-to-Spend*). |
| **Tableaux de Bord & Tableaux Croisés (TCD)** | Widgets visuels personnalisables (graphiques, barres, jauges, KPIs) et **tableaux croisés dynamiques** interactifs avec exploration détaillée des écritures et export CSV. |
| **Confort & Ergonomie** | Interface bilingue native (anglais / français), échelle typographique avec prévisualisation en direct, modes de tableau *Compact* vs *Confort* et personnalisation du jour d'ouverture du cycle budgétaire. |

---

## Démarrage Rapide

### 1. Bureau Windows (Superviseur System Tray)

Cadran inclut un superviseur de bureau Windows natif (`Cadran.Launcher.exe`) résidant dans votre zone de notification :
- Pilote le cycle de vie du serveur web Kestrel.
- Ouvre votre navigateur par défaut sur `http://localhost:5000` en un clic.
- Crée automatiquement des sauvegardes horodatées dans `app/backups/` au démarrage et à la fermeture.

```powershell
# Exécution depuis les sources
dotnet run --project Cadran.Launcher/Cadran.Launcher.csproj

# Ou génération du package de distribution autonome
./scripts/publish-windows.ps1
```

### 2. Binaire Autonome Unique Linux (`Cadran-*-linux-x64`)

**Quand l'utiliser :** Idéal pour les évaluations rapides, les postes de développement personnels, les scripts portables ou les serveurs ad-hoc. Il s'agit d'un exécutable unique, sans extraction préalable, embarquant le runtime .NET 9 complet et le moteur SQLCipher.

```bash
# 1. Télécharger le binaire de release et lui attribuer les droits d'exécution
chmod +x Cadran-1.0.0-linux-x64

# 2. Lancer directement (crée automatiquement ./data/cadran.db au premier démarrage)
./Cadran-1.0.0-linux-x64 --urls http://0.0.0.0:5000

# 3. Accéder à l'interface web
xdg-open http://localhost:5000 # ou ouvrir http://<ip-serveur>:5000
```

### 3. Archive de Déploiement Linux (`Cadran-*-linux-x64.tar.gz`) & `systemd`

**Quand l'utiliser :** Recommandé pour les environnements serveurs de production, serveurs domestiques et hébergement à long terme sous utilisateur système sans privilèges. L'archive se déploie dans une arborescence standardisée (`/opt/cadran`) avec les répertoires `data/` et `backups/` prêts à l'emploi.

```bash
# 1. Créer un utilisateur système dédié sans privilèges
sudo useradd --system --shell /usr/sbin/nologin --home-dir /opt/cadran cadran
sudo mkdir -p /opt/cadran
sudo chown -R cadran:cadran /opt/cadran

# 2. Extraire l'archive de distribution
sudo tar -xzf Cadran-1.0.0-linux-x64.tar.gz -C /opt/cadran
sudo chown -R cadran:cadran /opt/cadran
sudo chmod +x /opt/cadran/Cadran

# 3. Lancer directement ou déployer comme unité systemd durcie
sudo -u cadran /opt/cadran/Cadran --urls http://0.0.0.0:5000
```
*(Consultez les [Opérations de Production](docs/fr/user-guide.md#33-running-on-linux-production-deployments) pour le fichier d'unité systemd durci complet et le lancement automatique au démarrage).*

---

## Architecture & Frameworks Partagés

`Cadran` est articulé autour de bibliothèques modulaires réutilisables publiées sous l'écosystème `curvecoder`, conformes aux principes de la Clean Architecture et validées par les Quality Gates SonarQube :

- **[StatementForge](https://github.com/curvecoder/StatementForge)** (`v1.0.0`) : Parseur de relevés bancaires haute performance en flux continu (OFX, QFX, CSV) avec dédoublonnage de transactions, profils de mappage CSV personnalisés et détection des dépenses récurrentes.
- **[SqliteForge](https://github.com/curvecoder/SqliteForge)** (`v1.0.0`) : Infrastructure SQLite résiliente avec interception de la concurrence WAL, chiffrement AES-256 SQLCipher/SQLiteMC et fournisseurs de configuration dynamiques.
- **[LocaleForge](https://github.com/curvecoder/LocaleForge)** (`v1.0.0`) : Bibliothèque légère de localisation hiérarchique JSON et formateur de devises ISO-4217.

Toutes les dépendances sont restaurées automatiquement depuis GitHub Packages via le fichier `nuget.config` inclus.

Pour exécuter le pipeline complet de vérification qualité (formatage, compilation Release, tests unitaires et d'architecture avec couverture OpenCover, et Quality Gate SonarQube) :
```powershell
.\verify.ps1
```

---

## Support & Sponsoring

Cadran est un logiciel libre, gratuit et développé de manière indépendante. Si Cadran vous aide à gérer vos finances et à préserver votre souveraineté numérique, vous pouvez soutenir son développement :

[![Soutenir sur Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/curvecoder)

Vos contributions soutiennent directement la maintenance continue, les optimisations de performance et l'infrastructure open source.

---

## Licence

Ce projet est sous licence [MIT License](LICENSE) &copy; 2026 curvecoder.
