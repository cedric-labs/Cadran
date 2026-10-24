# Cadran - Personal Finance & Wealth Management

<p align="center">
  <img src="assets/cadran-logo.png" alt="Cadran Logo" width="220" />
  <br/>
  <a href="https://ko-fi.com/curvecoder"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Support curvecoder on Ko-fi" /></a>
</p>

<p align="center">
  <strong>A modern, local-first, privacy-first personal finance, budget tracking, and wealth management platform.</strong>
  <br/>
  Powered by ASP.NET Core 9, C# 12, Entity Framework Core, and Encrypted SQLite (SQLCipher).
</p>

---

## Documentation

- 📖 **[User Guide & Operations (docs/user-guide.md)](docs/user-guide.md)**: Comprehensive manual covering quickstart, step-by-step how-to recipes, Windows Tray Supervisor, Windows Service (NSSM), Linux `systemd`, Docker Compose, air-gapped deployments, and financial concepts.
- 🏗️ **[Architecture & Engineering Specification (docs/architecture.md)](docs/architecture.md)**: In-depth engineering reference detailing component architecture, decoupled shared frameworks (`StatementForge`, `SqliteForge`, `LocaleForge`, `FileForge`), threading and memory models, and two-tier security.

---

## Why Cadran?

Most personal finance applications require connecting your bank credentials to cloud aggregators, exposing financial transactions to third parties, or relying on subscription-based cloud services that can discontinue or leak sensitive data.

**Cadran is built on contrasting core principles:**
1. **True Local-First Data Sovereignty**: All your transactions, bank accounts, categories, and rules remain stored locally in a single SQLite database (`cadran.db`). No financial data ever leaves your machine.
2. **Zero Remote Scrapers or Third-Party Aggregators**: Retain complete control over your banking records by importing standard **OFX**, **QFX**, or **CSV** statements directly from your bank.
3. **Multi-User Isolation**: Host multiple isolated accounts on the same instance (family members or business vs. personal) with partitioned ledgers, rules, and dashboards.
4. **Two-Tier Security**: Account-level protection via password hashing with multi-user isolation combined with optional full data-at-rest encryption (**AES-256 / SQLCipher**). For remote access, connecting via a private VPN (such as WireGuard) is recommended.
5. **100% Offline & Air-Gapped Capable**: All web assets, icons, fonts, and scripts are embedded locally. No CDNs, tracking pixels, or internet access required.

---

## Key Features

| Domain | Capabilities |
|---|---|
| **Wealth & Accounts** | Consolidated net worth overview (Liquid Cash, Savings & Investments, Debt / Overdrafts), 16 preconfigured bank account types plus customizable statuses, multi-currency support (EUR, USD, GBP, CHF, CAD, etc.). |
| **Statement Import & Deduplication** | Streamlined ingestion of **OFX / QFX** (auto-association via `<ACCTID>`) and customizable **CSV** files (reusable mapping profiles). Strict duplicate prevention and interactive fuzzy-match resolution. |
| **Transaction Ledger & Splits** | Instant multi-criteria search, transaction splitting across multiple categories and payees, budget imputation date shift (allocating end-of-month paychecks to M+1), and batch reconciliation. |
| **Categories & Payees** | Two-level hierarchy (parent categories and subcategories), Income / Expense / Salary classifications, bilingual localization (English / French), and payee / merchant normalization. |
| **Rules Engine** | Priority-based categorization rules using wildcards (`*`, `%`, `?`) or regular expressions, account and amount filters, automated payee assignment, and retroactive application. |
| **Recurring Expenses & Safe-to-Spend** | Automated subscription and recurring bill detection with confidence scoring, status tracking (Debited, Pending, Overdue), and dynamic **Safe-to-Spend** forecasting. |
| **Dashboards & Pivot Tables (TCD)** | Customizable visual widgets (charts, bars, gauges, KPIs) and interactive multi-dimensional **Pivot Tables** with drill-down transaction inspection and CSV export. |
| **Appearance & Comfort** | Native bilingual interface (English / French), typography scaling with live preview, *Compact* vs. *Comfort* table modes, and customizable budget cycle starting days. |

---

## Quickstart

### 1. Windows Desktop (System Tray Supervisor)

Cadran includes a native Windows desktop supervisor (`Cadran.Launcher.exe`) that sits in your notification area:
- Manages the Kestrel web server lifecycle.
- Launches your default browser to `http://localhost:5000` with one click.
- Automatically creates timestamped backups in `app/backups/` upon startup and shutdown.

```powershell
# Run from source
dotnet run --project Cadran.Launcher/Cadran.Launcher.csproj

# Or build the standalone distribution package
./scripts/publish-windows.ps1
```

### 2. Docker & Docker Compose

Deploy Cadran inside a secure, lightweight Linux container (118 MB):

```bash
# Start container
docker compose up -d

# Access the interface
open http://localhost:8080
```

Persistent data and backups are preserved on the host via bind mounts in `./docker-data` and `./docker-backups`.

### 3. Linux / Headless Server (`systemd`)

Run Cadran as a background daemon on headless Linux servers:

```bash
# Build self-contained release
dotnet publish Ledger/Ledger.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /opt/cadran

# Launch directly
/opt/cadran/Cadran --urls http://0.0.0.0:5000
```
*(See [Production Operations](docs/user-guide.md#33-running-on-linux-systemd-service) for the complete hardened systemd unit file).*

---

## Support & Sponsoring

Cadran is free, open-source, and developed independently. If Cadran helps you organize your finances and protect your data sovereignty, consider supporting ongoing development:

[![Support on Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/curvecoder)

Your contributions directly support continuous maintenance, performance optimizations, and open-source infrastructure.

---

## License

This project is licensed under the [MIT License](LICENSE) &copy; 2026 curvecoder.
