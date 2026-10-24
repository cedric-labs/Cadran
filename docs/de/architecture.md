# Cadran - Architektur- & Engineering-Spezifikation

<p align="center">
  <strong>Sprachen:</strong>
  <a href="../architecture.md">English</a> |
  <a href="../fr/architecture.md">Français</a> |
  <a href="../es/architecture.md">Español</a> |
  <strong>Deutsch</strong>
</p>

## 1. Zusammenfassung der Architektur

**Cadran** ist eine hochperformante, lokale und datenschutzorientierte Plattform für persönliche Finanzen und Vermögensverwaltung, basierend auf ASP.NET Core 9 und modernem C# 12. Konsequent nach den Paradigmen **Local-First** und **Privacy-First** konzipiert, arbeitet Cadran vollständig autark ohne Cloud-Aggregationsdienste, Remote-Telemetrie oder Drittanbieter-Analysen.

Die Plattform besteht im Kern aus zwei Binärdateien:
1. **Cadran (Webserver-Engine)**: Eine ASP.NET Core 9-Anwendung mit reaktiver Weboberfläche, Fachlogik-Engine, transaktionalem Buchungsjournal und einer verschlüsselten SQLite-Speicherschicht.
2. **Cadran.Launcher (Windows-Supervisor)**: Ein schlankes Hilfsprogramm für den Windows-Infobereich, das den Lebenszyklus des Webservers überwacht, automatisierte Sicherungen beim Start und Beenden verwaltet und den Standard-Webbrowser öffnet.

```mermaid
flowchart TD
    subgraph ClientLayer["Präsentations- & Zugriffsschicht"]
        Browser["Moderner Webbrowser<br/>(Desktop / Mobil)"]
        TraySupervisor["Cadran.Launcher (WinForms)<br/>System-Tray-Supervisor"]
    end

    subgraph HostingLayer["Hosting & Prozessüberwachung"]
        KestrelServer["Kestrel-Webserver (ASP.NET Core 9)<br/>HTTP-Pipeline & Middleware"]
        AuthSystem["Identitäts- & Sicherheitssystem<br/>Cookie-Authentifizierung + TOTP 2FA"]
    end

    subgraph CoreEngine["Anwendungs- & Fachlogik-Engine"]
        LedgerService["Hauptbuch- & Buchungs-Engine<br/>Splitting, Imputation, Budgetzeiträume"]
        RulesEngine["Musterbasierte Regel-Engine<br/>Prioritätsabgleich & Automatisierung"]
        RecurringEngine["Engine für wiederkehrende Ausgaben<br/>Zuverlässigkeit & Verfügbares Budget"]
        ReportPivot["Pivot-Engine (TCD)<br/>Mehrdimensionale Aggregationen"]
    end

    subgraph Frameworks["Entkoppelte Shared Frameworks"]
        SF["StatementForge<br/>OFX / QFX / CSV-Parsing & Deduplizierung"]
        LF["LocaleForge<br/>Hierarchische Lokalisierung & ISO-4217"]
        SQF["SqliteForge<br/>WAL-Optimierung & Verschlüsselungs-Abfangung"]
        FF["FileForge<br/>Atomare I/O, sichere Backups & Sync"]
    end

    subgraph StorageLayer["Daten- & Persistenzschicht"]
        EFCore["Entity Framework Core 9<br/>Relationales Datenmodell & Migrationen"]
        SQLiteMC["SQLite Multiple Ciphers (libe_sqlite3mc)<br/>AES-256 GCM / CBC-Verschlüsselung"]
        DatabaseFile[("Verschlüsselte Datenbank<br/>cadran.db + WAL + SHM")]
    end

    Browser -->|HTTP localhost:5000 / 8080| KestrelServer
    TraySupervisor -->|Prozessverwaltung & Integritätsprüfung| KestrelServer
    TraySupervisor -->|Automatische Sicherungen & Rotation| FF
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

## 2. Architektur der entkoppelten Shared Frameworks

Cadran folgt den Prinzipien der Clean Architecture und der Dependency Inversion. Fachdomäne, Parsing, Speicherung und Lokalisierung nutzen wiederverwendbare modulare Bibliotheken aus dem `curvecoder`-Ökosystem:

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

### 2.1. Aufgaben der Frameworks
- **StatementForge**: Bietet performante Streaming-Parser für Open Financial Exchange (OFX 1.x und 2.x), Quicken Financial Exchange (QFX) und anpassbare getrennte Formate (CSV/TSV). Implementiert Fuzzy-Matching-Algorithmen zur Duplikatbereinigung und berechnet Frequenzmuster wiederkehrender Ausgaben.
- **SqliteForge**: Konfiguriert native SQLite-Parameter, aktiviert Write-Ahead-Logging (`PRAGMA journal_mode=WAL`), erzwingt relationale Integrität (`PRAGMA foreign_keys=ON`) und bindet `libe_sqlite3mc` für die Passphrasen-Verschlüsselung an ASP.NET Core an.
- **LocaleForge**: Liefert reflexionsfreie, hierarchische JSON-String-Lokalisierung für vollständige Zweisprachigkeit (`en-US` und `fr-FR`), gekoppelt mit ISO-4217-Währungsformatierung über alle Konten hinweg.
- **FileForge**: Verantwortlich für atomare Dateisystemoperationen, rollierende zeitgestempelte Zip-Sicherungen und absturzsichere Speicherschreibvorgänge.

---

## 3. Speicher-, Verschlüsselungs- & Sicherheitsarchitektur

### 3.1. Zweistufiges Sicherheitsmodell
Cadran trennt Daten und Authentifizierung über zwei unabhängige Schutzgrenzen:

```mermaid
flowchart LR
    subgraph Tier1["Stufe 1 - Benutzer-Authentifizierungsgrenze"]
        PasswordHash["ASP.NET Core Identity<br/>PBKDF2-Passwort-Hashing"]
        TOTP["RFC 6238 TOTP-Engine<br/>2FA QR-Code & Notfallschlüssel"]
        AuthCookie["Verschlüsseltes Session-Cookie<br/>HTTP-Only SameSite=Lax"]
    end

    subgraph Tier2["Stufe 2 - Verschlüsselungsgrenze im Ruhezustand"]
        MasterKey["Master-Passphrase<br/>Vom Benutzer bereitgestellt"]
        KeyDerivation["Argon2id / PBKDF2-HMAC-SHA256<br/>Schlüsselableitungsfunktion"]
        SQLCipher["SQLite Multiple Ciphers<br/>ChaCha20-Poly1305 / AES-256"]
        CipheredPages[("Verschlüsselte Datenbankseiten<br/>cadran.db auf Datenträger")]
    end

    PasswordHash --> AuthCookie
    TOTP --> AuthCookie
    MasterKey --> KeyDerivation
    KeyDerivation --> SQLCipher
    SQLCipher --> CipheredPages
```

1. **Benutzer-Authentifizierung (Stufe 1)**:
   - Jeder registrierte Benutzer verfügt über einen isolierten Bereich (`UserId`).
   - Passwörter werden mit ASP.NET Core Identity PBKDF2 und kryptografischen Salts geschützt.
   - **TOTP Zwei-Faktor-Authentifizierung (Nicht getestet / Geplante Entfernung)**: Die zeitbasierte Einmalpasswort-Authentifizierung (TOTP) wurde ursprünglich mit Offline-Wiederherstellungsschlüsseln implementiert. Eine direkte Veröffentlichung von Cadran im offenen Internet wird jedoch nicht empfohlen. Da Cadran für den lokalen Betrieb oder innerhalb eines geschützten Heimnetzwerks konzipiert ist, ist TOTP 2FA **nicht getestet und wird in einer künftigen Version entfernt**. Benutzer, die Fernzugriff benötigen, sollten sich über ein privates VPN (wie WireGuard) verbinden, anstatt Ports ins Internet weiterzuleiten.
2. **Datenbankverschlüsselung im Ruhezustand (Stufe 2)**:
   - Optionale vollständige Verschlüsselung auf Datenbankebene mittels **SQLite Multiple Ciphers (SQLCipher / AES-256)**.
   - Im aktivierten Zustand sind Rohdaten auf der Festplatte ohne den Master-Schlüssel unlesbar.
   - Die transparente In-Memory-Entschlüsselung übernimmt `libe_sqlite3mc` direkt auf C-Laufzeitebene, ohne unverschlüsselte Zwischendateien anzulegen.

### 3.2. SQLite-Parallelität & WAL-Modus
- Der Verbindungspool nutzt `journal_mode=WAL` (Write-Ahead Logging) und `synchronous=NORMAL`.
- Leser blockieren niemals Schreiber, und Schreiber blockieren niemals Leser.
- Hintergrund-Checkpoints überführen protokollierte Daten aus `-wal` und `-shm` regelmäßig in `cadran.db`, um ein unkontrolliertes Anwachsen der Logdateien zu verhindern.

---

## 4. Threading-Modell & Speicherverwaltung

### 4.1. Asynchrone I/O-Pipeline
- Alle Controller-Aktionen und EF Core-Abfragen nutzen kooperative `async/await`-Muster mit konsequenter Weitergabe von `CancellationToken`.
- HTTP-Anfragen werden über den .NET-Thread-Pool verarbeitet, ohne synchrone Blockaden (`.Result` oder `.Wait()` sind strikt untersagt).

### 4.2. Speicherallokation & Garbage Collection
- Das Parsen von Kontoauszügen (OFX/CSV) nutzt speicherabgebildete Streams und `Span<T>` / `ReadOnlySpan<char>`, um eine Fragmentierung des Large Object Heap (LOH) zu vermeiden.
- Aggregationsabfragen in Pivot-Tabellen (TCD) verwenden direkte Datenbank-Streaming-Projektionen (`Select`), um große Entitätsmengen nicht in den Arbeitsspeicher zu laden.
- Der Server arbeitet sehr ressourcenschonend (üblicherweise unter **90 MB** privater Arbeitssatz bei stabiler Last).

---

## 5. Performance & Benchmarks

| Metrik | Zielwert | Beobachtet |
|---|---|---|
| **Kaltstart des Servers** | < 1,5 Sekunden | ~ 850 ms (Release x64) |
| **OFX-Import (10.000 Buchungen)** | < 1,0 Sekunde | ~ 320 ms |
| **Regelausführungs-Engine (5.000 Regeln)** | < 100 ms | ~ 35 ms |
| **Pivot-Tabellen-Aggregation (50.000 Buchungen)** | < 250 ms | ~ 95 ms |
| **Standalone-Binary-Dateigröße** | < 150 MB | ~ 100 MB (Self-contained x64) |
