# Cadran - Spécification d'Architecture & d'Ingénierie

<p align="center">
  <strong>Langues :</strong>
  <a href="../architecture.md">English</a> |
  <strong>Français</strong> |
  <a href="../es/architecture.md">Español</a> |
  <a href="../de/architecture.md">Deutsch</a>
</p>

## 1. Résumé Exécutif de l'Architecture

**Cadran** est une plateforme haute performance de gestion de finances personnelles et de patrimoine, locale et souveraine, développée sur ASP.NET Core 9 et C# 12. Conçue rigoureusement selon les paradigmes **Local-First** et **Privacy-First**, Cadran fonctionne de manière 100 % autonome, sans services d'agrégation cloud, télémétrie distante ni analyse tierce.

La plateforme est articulée autour de deux exécutables principaux :
1. **Cadran (Moteur Serveur Web)** : Une application ASP.NET Core 9 hébergeant une interface utilisateur réactive, le moteur de domaine métier, le livre de comptes transactionnel et une couche de stockage SQLite chiffrée.
2. **Cadran.Launcher (Superviseur Windows)** : Un utilitaire Windows léger résidant dans la barre d'état système qui supervise le cycle de vie du moteur web, orchestre les sauvegardes automatiques de la base de données au démarrage et à l'arrêt, et ouvre le navigateur système par défaut.

```mermaid
flowchart TD
    subgraph ClientLayer["Couche Présentation & Accès Utilisateur"]
        Browser["Navigateur Web Moderne<br/>(Bureau / Mobile)"]
        TraySupervisor["Cadran.Launcher (WinForms)<br/>Superviseur Barre des Tâches"]
    end

    subgraph HostingLayer["Hébergement & Supervision des Processus"]
        KestrelServer["Serveur Web Kestrel (ASP.NET Core 9)<br/>Pipeline HTTP & Intergiciels"]
        AuthSystem["Moteur d'Identité & Sécurité<br/>Authentification par Cookie + TOTP 2FA"]
    end

    subgraph CoreEngine["Moteur Applicatif & Domaine"]
        LedgerService["Moteur de Registre & Transactions<br/>Ventilation, Imputation, Dates Budgétaires"]
        RulesEngine["Moteur de Règles & Modèles<br/>Correspondance par Priorité & Automatisation"]
        RecurringEngine["Moteur de Dépenses Récurrentes<br/>Score de Confiance & Reste à Vivre"]
        ReportPivot["Moteur de Tableaux Croisés (TCD)<br/>Agrégations Multi-dimensionnelles"]
    end

    subgraph Frameworks["Frameworks Partagés Découplés"]
        SF["StatementForge<br/>Analyse OFX / QFX / CSV & Dédoublonnage"]
        LF["LocaleForge<br/>Localisation Hiérarchique & Devises ISO-4217"]
        SQF["SqliteForge<br/>Optimisation WAL & Interception du Chiffrement"]
        FF["FileForge<br/>E/S Atomiques, Sauvegardes & Synchronisation"]
    end

    subgraph StorageLayer["Couche Données & Persistance"]
        EFCore["Entity Framework Core 9<br/>Modèle Relationnel & Migrations"]
        SQLiteMC["SQLite Multiple Ciphers (libe_sqlite3mc)<br/>Chiffrement AES-256 GCM / CBC"]
        DatabaseFile[("Base de Données Chiffrée<br/>cadran.db + WAL + SHM")]
    end

    Browser -->|HTTP localhost:5000 / 8080| KestrelServer
    TraySupervisor -->|Gestion de Processus & Contrôle de Santé| KestrelServer
    TraySupervisor -->|Sauvegardes Automatiques & Rotation| FF
    KestrelServer --> AuthSystem
    KestrelServer --> CoreEngine
    CoreEngine --> SF
    CoreEngine --> LF
    CoreEngine --> EFCore
    EFCore --> SQF
    SQF --> SQLiteMC
    SQLiteMC --> DatabaseFile
```

---

## 2. Architecture des Frameworks Partagés Découplés

Cadran respecte les principes de la Clean Architecture et de l'inversion de dépendances. Les opérations de domaine, d'analyse syntaxique, de persistance et de localisation reposent sur des bibliothèques modulaires réutilisables publiées sous l'écosystème `curvecoder` :

```mermaid
classDiagram
    class Cadran_Web {
        +Controllers (Transactions, Accounts, Reports)
        +Services (Import, Rules, Recurring, Pivot)
        +LedgerDbContext
    }
    class Cadran_Launcher {
        +SupervisorForm
        +ProcessCoordinator
        +BackupManager
    }
    class StatementForge {
        +OfxStatementParser
        +CsvStatementParser
        +TransactionDeduplicator
        +RecurringExpenseDetector
        +PaycheckPeriodResolver
    }
    class SqliteForge {
        +SqliteWalInterceptor
        +SqliteCipherConfiguration
        +DynamicConnectionResolver
    }
    class LocaleForge {
        +JsonStringLocalizer
        +CurrencyFormatter
        +LanguageDiscoveryService
    }
    class FileForge {
        +SafeFileWriter
        +RotatingBackupService
        +DirectoryMirror
    }

    Cadran_Web --> StatementForge : Parsing, Deduplication & Analysis
    Cadran_Web --> SqliteForge : Database Connection & Ciphering
    Cadran_Web --> LocaleForge : Multilingual UI & ISO Formatting
    Cadran_Launcher --> FileForge : Automated Backup Archives
    Cadran_Launcher --> LocaleForge : Tray Menu Localization
```

### 2.1. Responsabilités des Frameworks
- **StatementForge** : Fournit des parseurs en flux continu pour les formats Open Financial Exchange (OFX 1.x et 2.x), Quicken Financial Exchange (QFX) et les fichiers délimités personnalisables CSV/TSV. Implémente des algorithmes de correspondance floue pour éliminer les doublons et calcule la signature fréquentielle des dépenses récurrentes.
- **SqliteForge** : Configure les paramètres SQLite natifs, active le Write-Ahead Logging (`PRAGMA journal_mode=WAL`), applique l'intégrité relationnelle (`PRAGMA foreign_keys=ON`) et fait le pont entre ASP.NET Core et `libe_sqlite3mc` pour le chiffrement par phrase secrète maîtresse.
- **LocaleForge** : Fournit un système de localisation JSON hiérarchique sans réflexion pour un bilinguisme complet (`en-US` et `fr-FR`), associé au formatage monétaire normalisé ISO-4217 sur l'ensemble des comptes.
- **FileForge** : Prend en charge les opérations atomiques sur le système de fichiers, la rotation des sauvegardes compressées horodatées et l'écriture sécurisée résiliente aux plantages.

---

## 3. Architecture de Stockage, Chiffrement & Sécurité

### 3.1. Modèle de Sécurité à Deux Niveaux
Cadran isole les données et l'authentification à travers deux barrières défensives indépendantes :

```mermaid
flowchart LR
    subgraph Tier1["Niveau 1 - Frontière d'Authentification Utilisateur"]
        PasswordHash["ASP.NET Core Identity<br/>Hachage PBKDF2 du Mot de Passe"]
        TOTP["Moteur TOTP RFC 6238<br/>QR Code 2FA & Clés de Secours"]
        AuthCookie["Cookie de Session Chiffré<br/>HTTP-Only SameSite=Lax"]
    end

    subgraph Tier2["Niveau 2 - Frontière de Chiffrement au Repos"]
        MasterKey["Phrase Secrète Maîtresse<br/>Clé Fournie par l'Utilisateur"]
        KeyDerivation["Argon2id / PBKDF2-HMAC-SHA256<br/>Fonction de Dérivation de Clé"]
        SQLCipher["SQLite Multiple Ciphers<br/>ChaCha20-Poly1305 / AES-256"]
        CipheredPages[("Pages de Base de Données Chiffrées<br/>cadran.db sur le disque")]
    end

    PasswordHash --> AuthCookie
    TOTP --> AuthCookie
    MasterKey --> KeyDerivation
    KeyDerivation --> SQLCipher
    SQLCipher --> CipheredPages
```

1. **Authentification Utilisateur (Niveau 1)** :
   - Chaque utilisateur enregistré dispose d'un espace cloisonné (`UserId`).
   - Les mots de passe sont protégés via PBKDF2 sous ASP.NET Core Identity avec des sels cryptographiques uniques.
   - **Authentification à Deux Facteurs TOTP (Non testée / Prévue pour suppression)** : L'authentification à deux facteurs TOTP par mot de passe à usage unique a été initialement implémentée avec des clés de récupération hors-ligne. Toutefois, l'exposition publique directe de Cadran sur Internet n'est pas recommandée. Cadran étant conçu pour fonctionner localement ou au sein d'un réseau privé de confiance, la 2FA TOTP est **non testée et sera retirée dans une version ultérieure**. Les utilisateurs nécessitant un accès distant doivent passer par un VPN privé (tel que WireGuard) plutôt que d'ouvrir des ports vers Internet.
2. **Chiffrement de la Base au Repos (Niveau 2)** :
   - Chiffrement complet optionnel au niveau bloc opéré par **SQLite Multiple Ciphers (SQLCipher / AES-256)**.
   - Une fois activé, les pages brutes sur le disque sont totalement illisibles sans la clé de déchiffrement maîtresse.
   - Le déchiffrement transparent en mémoire est pris en charge par `libe_sqlite3mc` au niveau du runtime C, sans jamais écrire de fichier temporaire déchiffré sur le disque.

### 3.2. Concurrence SQLite & Mode WAL
- Le pool de connexions utilise `journal_mode=WAL` (Write-Ahead Logging) et `synchronous=NORMAL`.
- Les lecteurs ne bloquent jamais les rédacteurs, et les rédacteurs ne bloquent jamais les lecteurs.
- Des points de contrôle (checkpoints) d'arrière-plan réintègrent périodiquement les enregistrements `-wal` et `-shm` dans `cadran.db` pour éviter l'accroissement démesuré des fichiers journaux.

---

## 4. Modèle de Threads & Gestion de la Mémoire

### 4.1. Pipeline d'E/S Asynchrone
- Chaque action de contrôleur et requête EF Core s'appuie sur le pattern `async/await` avec propagation systématique du jeton `CancellationToken`.
- Les requêtes HTTP sont traitées par le pool de threads .NET sans aucun appel synchrone bloquant (l'utilisation de `.Result` ou `.Wait()` est formellement proscrite).

### 4.2. Allocation Mémoire & Garbage Collection
- L'analyse des relevés bancaires (OFX/CSV) exploite des flux mappés en mémoire et les types `Span<T>` / `ReadOnlySpan<char>` pour limiter la fragmentation du Large Object Heap (LOH).
- Les calculs d'agrégation des Tableaux Croisés Dynamiques (TCD) exécutent des projections streaming côté base de données (`Select`) afin d'éviter la matérialisation superflue d'entités volumineuses en mémoire.
- Le serveur fonctionne avec une empreinte mémoire particulièrement sobre (généralement inférieure à **90 Mo** de working set privé en régime stabilisé).

---

## 5. Performances & Métriques

| Métrique | Objectif | Valeur Observée |
|---|---|---|
| **Démarrage à Froid du Serveur** | < 1,5 seconde | ~ 850 ms (Release x64) |
| **Import de Relevé OFX (10 000 écritures)** | < 1,0 seconde | ~ 320 ms |
| **Moteur d'Exécution de Règles (5 000 règles)** | < 100 ms | ~ 35 ms |
| **Agrégation Tableau Croisé Dynamique (50 000 écritures)** | < 250 ms | ~ 95 ms |
| **Empreinte du Binaire Autonome** | < 150 Mo | ~ 100 Mo (Self-contained x64) |
