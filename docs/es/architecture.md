# Cadran - Especificación de Arquitectura e Ingeniería

<p align="center">
  <strong>Idiomas:</strong>
  <a href="../architecture.md">English</a> |
  <a href="../fr/architecture.md">Français</a> |
  <strong>Español</strong> |
  <a href="../de/architecture.md">Deutsch</a>
</p>

## 1. Resumen Ejecutivo de la Arquitectura

**Cadran** es una plataforma de alto rendimiento de finanzas personales y gestión patrimonial, diseñada para operar localmente y con estricto respeto a la privacidad, construida sobre ASP.NET Core 9 y C# 12. Diseñada bajo los paradigmas **Local-First** y **Privacy-First**, Cadran funciona de forma completamente autónoma sin servicios de agregación en la nube, telemetría remota ni analítica de terceros.

La plataforma está estructurada en torno a dos binarios principales:
1. **Cadran (Motor Servidor Web)**: Una aplicación ASP.NET Core 9 que aloja una interfaz web interactiva, el motor de dominio empresarial, el libro mayor transaccional y una capa de almacenamiento SQLite cifrada.
2. **Cadran.Launcher (Supervisor de Windows)**: Una utilidad ligera en la bandeja del sistema de Windows que supervisa el ciclo de vida del motor web, gestiona copias de seguridad automáticas al iniciar y al cerrar, y abre el navegador del sistema predeterminado.

```mermaid
flowchart TD
    subgraph ClientLayer["Capa de Presentación y Acceso de Usuario"]
        Browser["Navegador Web Moderno<br/>(Escritorio / Móvil)"]
        TraySupervisor["Cadran.Launcher (WinForms)<br/>Supervisor en Bandeja del Sistema"]
    end

    subgraph HostingLayer["Alojamiento y Supervisión de Procesos"]
        KestrelServer["Servidor Web Kestrel (ASP.NET Core 9)<br/>Canal HTTP y Middleware"]
        AuthSystem["Motor de Identidad y Seguridad<br/>Autenticación por Cookie + TOTP 2FA"]
    end

    subgraph CoreEngine["Motor de Aplicación y Dominio"]
        LedgerService["Motor de Libro Mayor y Transacciones<br/>Desglose, Imputación, Fechas Presupuestarias"]
        RulesEngine["Motor de Reglas y Patrones<br/>Coincidencia por Prioridad y Automatización"]
        RecurringEngine["Motor de Gastos Periódicos<br/>Puntuación de Confianza y Margen Disponible"]
        ReportPivot["Motor de Tablas Dinámicas (TCD)<br/>Agregaciones Multidimensionales"]
    end

    subgraph Frameworks["Frameworks Compartidos Desacoplados"]
        SF["StatementForge<br/>Análisis OFX / QFX / CSV y Deduplicación"]
        LF["LocaleForge<br/>Localización Jerárquica y Divisas ISO-4217"]
        SQF["SqliteForge<br/>Optimización WAL e Intercepción de Cifrado"]
        FF["FileForge<br/>E/S Atómica, Copias de Seguridad y Sincronización"]
    end

    subgraph StorageLayer["Capa de Datos y Persistencia"]
        EFCore["Entity Framework Core 9<br/>Modelo Relacional y Migraciones"]
        SQLiteMC["SQLite Multiple Ciphers (libe_sqlite3mc)<br/>Cifrado AES-256 GCM / CBC"]
        DatabaseFile[("Base de Datos Cifrada<br/>cadran.db + WAL + SHM")]
    end

    Browser -->|HTTP localhost:5000 / 8080| KestrelServer
    TraySupervisor -->|Gestión de Procesos y Control de Estado| KestrelServer
    TraySupervisor -->|Copias Automáticas y Rotación| FF
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

## 2. Arquitectura de Frameworks Compartidos Desacoplados

Cadran aplica principios de Clean Architecture e inversión de dependencias. Las operaciones de dominio, análisis sintáctico, persistencia y localización descansan en librerías modulares reutilizables publicadas en el ecosistema `curvecoder`:

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

### 2.1. Responsabilidades de los Frameworks
- **StatementForge**: Proporciona analizadores de flujo continuo para formatos Open Financial Exchange (OFX 1.x y 2.x), Quicken Financial Exchange (QFX) y formatos delimitados configurables CSV/TSV. Implementa algoritmos de coincidencia difusa para eliminar duplicados y calcula firmas de frecuencia de gastos recurrentes.
- **SqliteForge**: Configura parámetros nativos de SQLite, habilita Write-Ahead Logging (`PRAGMA journal_mode=WAL`), asegura la integridad referencial (`PRAGMA foreign_keys=ON`) y conecta ASP.NET Core con `libe_sqlite3mc` para el cifrado mediante frase de contraseña maestra.
- **LocaleForge**: Ofrece un sistema de localización jerárquica de cadenas JSON sin reflexión para un bilingüismo completo (`en-US` y `fr-FR`), junto con formato monetario estandarizado ISO-4217 en múltiples cuentas.
- **FileForge**: Gestiona operaciones atómicas en el sistema de archivos, rotación de copias de seguridad comprimidas con marca de tiempo y escrituras seguras resistentes a fallos.

---

## 3. Arquitectura de Almacenamiento, Cifrado y Seguridad

### 3.1. Modelo de Seguridad en Dos Niveles
Cadran aísla los datos y la autenticación mediante dos barreras de seguridad independientes:

```mermaid
flowchart LR
    subgraph Tier1["Nivel 1 - Límite de Autenticación de Usuario"]
        PasswordHash["ASP.NET Core Identity<br/>Hash PBKDF2 de Contraseña"]
        TOTP["Motor TOTP RFC 6238<br/>Código QR 2FA y Claves de Recuperación"]
        AuthCookie["Cookie de Sesión Cifrada<br/>HTTP-Only SameSite=Lax"]
    end

    subgraph Tier2["Nivel 2 - Límite de Cifrado en Reposo"]
        MasterKey["Frase de Contraseña Maestra<br/>Clave Suministrada por el Usuario"]
        KeyDerivation["Argon2id / PBKDF2-HMAC-SHA256<br/>Función de Derivación de Clave"]
        SQLCipher["SQLite Multiple Ciphers<br/>ChaCha20-Poly1305 / AES-256"]
        CipheredPages[("Páginas de Base de Datos Cifradas<br/>cadran.db en disco")]
    end

    PasswordHash --> AuthCookie
    TOTP --> AuthCookie
    MasterKey --> KeyDerivation
    KeyDerivation --> SQLCipher
    SQLCipher --> CipheredPages
```

1. **Autenticación de Usuario (Nivel 1)**:
   - Cada usuario registrado posee un espacio aislado (`UserId`).
   - Las contraseñas se protegen con PBKDF2 bajo ASP.NET Core Identity con sales criptográficas únicas.
   - **Autenticación en Dos Pasos TOTP (No probada / Prevista para obsolescencia)**: La autenticación de dos factores basada en tiempo (TOTP) se implementó inicialmente con claves de recuperación sin conexión. Sin embargo, no se recomienda la exposición web pública directa de Cadran. Dado que Cadran está diseñado para ejecutarse localmente o dentro de una red privada de confianza, TOTP 2FA está **sin pruebas y se eliminará en una versión futura**. Los usuarios que requieran acceso remoto deben conectarse a través de una VPN privada (como WireGuard) en lugar de redirigir puertos a internet.
2. **Cifrado de Base de Datos en Reposo (Nivel 2)**:
   - Cifrado completo a nivel de bloque opcional mediante **SQLite Multiple Ciphers (SQLCipher / AES-256)**.
   - Una vez habilitado, las páginas en disco son totalmente ilegibles sin la clave de cifrado maestra.
   - El descifrado transparente en memoria lo realiza directamente `libe_sqlite3mc` a nivel del runtime de C sin crear archivos temporales sin cifrar en disco.

### 3.2. Concurrencia de SQLite y Modo WAL
- El grupo de conexiones utiliza `journal_mode=WAL` (Write-Ahead Logging) y `synchronous=NORMAL`.
- Los lectores nunca bloquean a los escritores, y los escritores nunca bloquean a los lectores.
- Los puntos de control periódicos reintegran los registros `-wal` y `-shm` en `cadran.db` para evitar el crecimiento excesivo de archivos de registro.

---

## 4. Modelo de Hilos y Gestión de Memoria

### 4.1. Canal de E/S Asíncrono
- Cada endpoint de controlador y consulta de EF Core implementa el patrón cooperativo `async/await` con propagación constante de `CancellationToken`.
- Las solicitudes HTTP son atendidas por el thread pool de .NET sin bloqueos síncronos (el uso de `.Result` o `.Wait()` está estrictamente prohibido).

### 4.2. Asignación de Memoria y Recolección de Basura
- El análisis de extractos (OFX/CSV) utiliza flujos mapeados en memoria y particionamiento mediante `Span<T>` / `ReadOnlySpan<char>` para evitar la fragmentación del Large Object Heap (LOH).
- Las agregaciones en Tablas Dinámicas (TCD) ejecutan proyecciones directas en la base de datos (`Select`) para no materializar colecciones grandes en memoria.
- El servidor mantiene un consumo de memoria notablemente reducido (habitualmente por debajo de **90 MB** de conjunto de trabajo privado bajo carga estable).

---

## 5. Rendimiento y Métricas

| Métrica | Objetivo | Observado |
|---|---|---|
| **Arranque en Frío del Servidor** | < 1,5 segundos | ~ 850 ms (Release x64) |
| **Importación de Extracto OFX (10.000 tx)** | < 1,0 segundo | ~ 320 ms |
| **Motor de Reglas (5.000 reglas)** | < 100 ms | ~ 35 ms |
| **Agregación en Tabla Dinámica (50.000 tx)** | < 250 ms | ~ 95 ms |
| **Tamaño del Binario Autónomo** | < 150 MB | ~ 100 MB (Self-contained x64) |
