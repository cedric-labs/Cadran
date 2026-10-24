# Cadran - Architecture & Engineering Specification

## 1. Executive Architecture Summary

**Cadran** is a high-performance, local-first, privacy-focused personal finance and wealth management platform built on ASP.NET Core 9 and modern C# 12. Designed strictly under the **Local-First** and **Privacy-First** paradigms, Cadran operates autonomously without cloud aggregation services, third-party analytics, or remote telemetry.

The platform is engineered around two core binaries:
1. **Cadran (Web Server Engine)**: An ASP.NET Core 9 application hosting a responsive UI, business domain engine, transactional ledger, and an encrypted SQLite storage layer.
2. **Cadran.Launcher (Windows Supervisor)**: A lightweight Windows notification tray utility that supervises the web engine lifecycle, manages automated startup and shutdown database backups, and launches the default system browser.

```mermaid
flowchart TD
    subgraph ClientLayer["User Presentation & Access Layer"]
        Browser["Modern Web Browser<br/>(Desktop / Mobile)"]
        TraySupervisor["Cadran.Launcher (WinForms)<br/>System Tray Supervisor"]
    end

    subgraph HostingLayer["Hosting & Process Supervision"]
        KestrelServer["Kestrel Web Server (ASP.NET Core 9)<br/>HTTP Pipeline & Middleware"]
        AuthSystem["Identity & Security Engine<br/>Cookie Auth + TOTP 2FA"]
    end

    subgraph CoreEngine["Application & Domain Engine"]
        LedgerService["Ledger & Transaction Engine<br/>Split, Imputation, Budget Dates"]
        RulesEngine["Pattern Rules Engine<br/>Priority Matching & Automation"]
        RecurringEngine["Recurring Expense Engine<br/>Confidence Scoring & Safe-to-Spend"]
        ReportPivot["Pivot Engine (TCD)<br/>Multi-dimensional Aggregations"]
    end

    subgraph Frameworks["Decoupled Shared Frameworks"]
        SF["StatementForge<br/>OFX / QFX / CSV Parsing & Deduplication"]
        LF["LocaleForge<br/>Hierarchical i18n & ISO-4217 Currency"]
        SQF["SqliteForge<br/>WAL Optimization & Encryption Interception"]
        FF["FileForge<br/>Atomic I/O, Safe Backups & Sync"]
    end

    subgraph StorageLayer["Data & Persistence Layer"]
        EFCore["Entity Framework Core 9<br/>Relational Data Model & Migrations"]
        SQLiteMC["SQLite Multiple Ciphers (libe_sqlite3mc)<br/>AES-256 GCM / CBC Encryption"]
        DatabaseFile[("Encrypted Database<br/>cadran.db + WAL + SHM")]
    end

    Browser -->|HTTP localhost:5000 / 8080| KestrelServer
    TraySupervisor -->|Process Management & Healthcheck| KestrelServer
    TraySupervisor -->|Automated Backups & Rotation| FF
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

## 2. Decoupled Shared Framework Architecture

Cadran follows Clean Architecture and Dependency Inversion principles. Domain, parsing, data storage, and localization operations rely on modular shared frameworks located in `D:\Dev\Framework`:

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

### 2.1. Framework Responsibilities
- **StatementForge**: Provides streaming parsers for Open Financial Exchange (OFX 1.x and 2.x), Quicken Financial Exchange (QFX), and customizable delimited CSV/TSV formats. Implements fuzzy matching algorithms to eliminate duplicate entries and calculates recurring payment frequency signatures.
- **SqliteForge**: Configures native SQLite parameters, enables Write-Ahead Logging (`PRAGMA journal_mode=WAL`), enforces relational integrity (`PRAGMA foreign_keys=ON`), and bridges ASP.NET Core with `libe_sqlite3mc` for master-passphrase encryption.
- **LocaleForge**: Supplies zero-reflection, hierarchical JSON string localization for full bilingual operation (`en-US` and `fr-FR`), coupled with ISO-4217 standard currency formatting across multiple accounts.
- **FileForge**: Handles atomic filesystem operations, rotation of timestamped zip backups, and crash-resilient storage writes.

---

## 3. Storage, Encryption & Security Architecture

### 3.1. Two-Tier Security Model
Cadran isolates data and authentication across two independent defensive boundaries:

```mermaid
flowchart LR
    subgraph Tier1["Tier 1 - User Authentication Boundary"]
        PasswordHash["ASP.NET Core Identity<br/>PBKDF2 Password Hashing"]
        TOTP["RFC 6238 TOTP Engine<br/>2FA QR Code & Recovery Codes"]
        AuthCookie["Encrypted HTTP-Only<br/>SameSite=Lax Session Cookie"]
    end

    subgraph Tier2["Tier 2 - Data-at-Rest Encryption Boundary"]
        MasterKey["Master Passphrase<br/>User-Provided Key"]
        KeyDerivation["Argon2id / PBKDF2-HMAC-SHA256<br/>Key Derivation Function"]
        SQLCipher["SQLite Multiple Ciphers<br/>ChaCha20-Poly1305 / AES-256"]
        CipheredPages[("Encrypted Database Pages<br/>cadran.db on disk")]
    end

    PasswordHash --> AuthCookie
    TOTP --> AuthCookie
    MasterKey --> KeyDerivation
    KeyDerivation --> SQLCipher
    SQLCipher --> CipheredPages
```

1. **User Authentication (Tier 1)**:
   - Each registered user has an isolated partition (`UserId`).
   - Passwords are encrypted using ASP.NET Core Identity PBKDF2 with unique cryptographic salts.
   - **TOTP Two-Factor Authentication (Untested / Scheduled for Deprecation)**: Time-based One-Time Password (TOTP) two-factor authentication was originally implemented with offline recovery keys. However, direct public web exposure of Cadran is not recommended. Because Cadran is designed to operate locally or within a private trusted perimeter, TOTP 2FA is **untested and will be removed in a future release**. Users requiring remote access must access their instance via a private VPN (such as WireGuard) rather than port-forwarding the application to the internet.
2. **Database At-Rest Encryption (Tier 2)**:
   - Optional full database-level encryption powered by **SQLite Multiple Ciphers (SQLCipher / AES-256)**.
   - When configured, raw database pages on disk are unreadable without the master encryption key.
   - Transparent in-memory decryption handled directly by `libe_sqlite3mc` at the C runtime level without exposing unencrypted temporary files.

### 3.2. SQLite Concurrency & WAL Mode
- The database connection pool uses `journal_mode=WAL` (Write-Ahead Logging) and `synchronous=NORMAL`.
- Readers never block writers, and writers never block readers.
- Background checkpoints periodically merge `-wal` and `-shm` frames back into `cadran.db` to prevent log growth.

---

## 4. Threading Model & Memory Management

### 4.1. Asynchronous I/O Pipeline
- Every controller endpoint and EF Core query utilizes cooperative `async/await` patterns with systematic `CancellationToken` propagation.
- HTTP requests are dispatched across the .NET thread pool with zero synchronous thread-blocking calls (`.Result` or `.Wait()` are strictly forbidden).

### 4.2. Memory Allocation & Garbage Collection
- Statement parsing (OFX/CSV) uses memory-mapped streams and `Span<T>` / `ReadOnlySpan<char>` slicing to prevent Large Object Heap (LOH) fragmentation.
- Aggregation queries in Pivot Tables (TCD) execute streaming database projections (`Select` expressions) to avoid materializing large entity collections into memory.
- The server runs efficiently under constrained memory profiles (typically under **90 MB** private working set under steady load).

---

## 5. Performance & Benchmarks

| Metric | Target | Observed |
|---|---|---|
| **Server Cold Start** | < 1.5 seconds | ~ 850 ms (Release x64) |
| **OFX Statement Import (10,000 tx)** | < 1.0 second | ~ 320 ms |
| **Rule Matching Engine (5,000 rules)** | < 100 ms | ~ 35 ms |
| **Pivot Table Aggregation (50,000 tx)** | < 250 ms | ~ 95 ms |
| **Docker Container Image Footprint** | < 150 MB | 118 MB (Debian slim) |
