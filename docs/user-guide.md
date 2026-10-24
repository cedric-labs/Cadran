# Cadran - User Guide & Production Operations

Welcome to the official user manual for **Cadran**, a modern, privacy-first personal finance, budget tracking, and wealth management platform.

This guide is structured according to the **Diátaxis** framework:
- 🎓 **[1. Getting Started Tutorial](#1-getting-started-tutorial)** *(Learning-oriented)*: A step-by-step walkthrough to configure your workspace, import your first statement, and inspect your financial health in 10 minutes.
- 🛠️ **[2. How-To Practical Guides](#2-how-to-practical-guides)** *(Task-oriented)*: Step-by-step recipes for day-to-day operations (CSV profile creation, receipt splitting, automation rules, encrypted backups).
- 🚀 **[3. Deployment & Production Operations](#3-deployment--production-operations)** *(Operational)*: Running the Windows Tray Supervisor, installing as a Windows Service with NSSM, configuring Linux systemd units, Docker Compose, and air-gapped environments.
- 💡 **[4. Key Concepts & Financial Rationale](#4-key-concepts--financial-rationale)** *(Understanding-oriented)*: Accounting principles, Real Date vs. Budget Imputation Date, Safe-to-Spend calculation, and the two-tier security model.
- 📖 **[5. Functional Reference](#5-functional-reference)** *(Information-oriented)*: Wildcard syntax, CSV import formats, and keyboard navigation.

---

## 1. Getting Started Tutorial

### 1.1. Starting the Application
1. **Launch the binary**:
   - On Windows: Double-click `Cadran.Launcher.exe` (or run `dotnet run --project Ledger/Ledger.csproj`).
   - On Linux / Docker: Execute `./Cadran` or `docker compose up -d`.
2. **Access the Web Interface**: Open your browser and navigate to `http://localhost:5000` (or `http://localhost:8080` in Docker).
3. **Select Language**: Click the language toggle (`EN` / `FR`) in the navigation bar.
4. **Register Your Account**:
   - Click **Create an Account**.
   - Enter your full name, email address, and a secure password (minimum 8 characters, with letters and numbers).
   - Upon creation, Cadran initializes a comprehensive default catalog of income and expense categories.

### 1.2. Creating Your First Account
1. From the navigation bar, go to **Accounts** (`/Accounts`).
2. Click **New Account**.
3. Specify your financial institution (e.g., `Chase`, `Barclays`, `BNP Paribas`, `BoursoBank`).
4. Set the **Account Name** (e.g., `Main Checking Account`).
5. Choose the **Account Type** (Checking, Savings, Investment, Credit Card) and **Currency** (e.g., `EUR`, `USD`, `GBP`).
6. Enter your **Initial Balance** prior to the first imported statement, then click **Save**.

### 1.3. Importing Your First Statement
1. Navigate to **Imports** (`/Import`).
2. Select your target account from the dropdown.
3. Choose your file format:
   - **OFX / QFX**: Native banking format with automatic `<ACCTID>` validation.
   - **CSV**: Configurable delimited text format.
4. Drag and drop your bank file.
5. Review the preview list and click **Confirm Import**. Cadran automatically screens for duplicates.

---

## 2. How-To Practical Guides

### 2.1. Splitting a Transaction Across Categories
When a single store receipt contains items that belong to different budget buckets (e.g., groceries and home office supplies):
1. Navigate to **Transactions** (`/Transactions`).
2. Click on the transaction row to open the editing drawer.
3. Click **Split Transaction**.
4. Specify each split amount, category, and optional sub-merchant.
5. The total of all split rows must match the original transaction amount.
6. Click **Save Splits**.

### 2.2. Automating Categorization with the Rules Engine
1. Go to **Settings > Automation Rules** (`/Rules`).
2. Click **New Rule**.
3. Define the criteria:
   - **Label Pattern**: Use wildcards (`*AMZN*`, `NETFLIX%`) or regular expressions (`^UBER\s*EATS`).
   - **Account Filter**: Restrict to a specific account or apply globally.
   - **Amount Range**: Optional minimum and maximum bounds.
4. Define the target action:
   - Assign Category and Subcategory.
   - Assign Payee / Merchant.
   - Optional: Toggle **Shift to Next Budget Month** (+1 month offset).
5. Check **Apply Retroactively to Existing Transactions** to reclassify past statements.
6. Click **Save Rule**.

### 2.3. Managing Recurring Charges & Safe-to-Spend
1. Navigate to **Recurring Charges** (`/Recurring`).
2. Review automatically detected subscriptions (gym memberships, streaming, rent, utilities).
3. Confirm or adjust payment frequency (Monthly, Quarterly, Annual) and expected due day.
4. The **Safe-to-Spend** metric dynamically deducts pending recurring obligations from your current balance as the month progresses.

### 2.4. Generating Dynamic Pivot Tables (TCD)
1. Go to **Reports > Pivot Table** (`/Reports/Pivot`).
2. Select your dimensions:
   - **Row Axis**: Categories, Merchants, or Accounts.
   - **Column Axis**: Month, Quarter, or Year.
   - **Values**: Net Total, Inflow, Outflow, or Transaction Count.
3. Click any cell to inspect the underlying transactions in a drill-down modal.
4. Export the resulting aggregation to CSV at any time.

---

## 3. Deployment & Production Operations

### 3.1. Windows Tray Supervisor (`Cadran.Launcher.exe`)
For desktop Windows environments, `Cadran.Launcher.exe` provides a seamless tray application:
- Resides quietly in the Windows notification area (near the clock).
- Automatically starts the Kestrel server and opens your default browser.
- Executes automated timestamped database backups upon launch and exit.
- Provides a one-click **View Logs** and **Restart Server** context menu.

### 3.2. Running as a Hardened Windows Service (NSSM)
To run Cadran as a persistent background service without an interactive user login, use **NSSM** (Non-Sucking Service Manager):

1. **Install NSSM**:
   ```powershell
   # Via winget
   winget install NSSM.NSSM

   # Via Chocolatey
   choco install nssm

   # Via Scoop
   scoop install nssm
   ```

2. **Configure and Install the Service**:
   ```powershell
   # Define paths
   $serviceName = "Cadran"
   $binaryPath = "C:\Cadran\app\Cadran.exe"
   $appDir = "C:\Cadran\app"

   # Install service
   nssm install $serviceName $binaryPath
   nssm set $serviceName AppDirectory $appDir
   nssm set $serviceName AppParameters "--urls http://0.0.0.0:5000"
   nssm set $serviceName Start SERVICE_AUTO_START

   # Set environment variables
   nssm set $serviceName AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production" "Server__AllowNetworkAccess=true"

   # Start service
   nssm start $serviceName
   ```

3. **Verify Status**:
   ```powershell
   Get-Service -Name Cadran
   ```

### 3.3. Running on Linux (`systemd` Service)
Deploy Cadran as a hardened, unprivileged systemd daemon on Linux servers:

1. **Create dedicated service user**:
   ```bash
   sudo useradd --system --shell /usr/sbin/nologin --home-dir /opt/cadran cadran
   sudo mkdir -p /opt/cadran/data /opt/cadran/backups
   sudo chown -R cadran:cadran /opt/cadran
   ```

2. **Extract release files**:
   ```bash
   tar -xzf Cadran-1.0.0-linux-x64.tar.gz -C /opt/cadran
   chmod +x /opt/cadran/Cadran
   ```

3. **Deploy systemd service unit (`/etc/systemd/system/cadran.service`)**:
   ```ini
   [Unit]
   Description=Cadran Personal Finance Web Application
   After=network.target

   [Service]
   Type=notify
   User=cadran
   Group=cadran
   WorkingDirectory=/opt/cadran
   ExecStart=/opt/cadran/Cadran --urls http://127.0.0.1:5000
   Restart=always
   RestartSec=10

   # Security hardening
   NoNewPrivileges=true
   ProtectSystem=full
   ProtectHome=true
   PrivateTmp=true
   ProtectControlGroups=true

   # Environment
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

   [Install]
   WantedBy=multi-user.target
   ```

4. **Enable and start**:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable --now cadran.service
   sudo systemctl status cadran.service
   ```

### 3.4. Docker & Docker Compose Deployment
```yaml
# docker-compose.yml
services:
  cadran:
    image: cadran:latest
    container_name: cadran
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Server__AllowNetworkAccess=true
    volumes:
      - ./docker-data:/app/data
      - ./docker-backups:/app/backups
```

```bash
# Build and run
docker compose up -d

# Check logs
docker compose logs -f
```

### 3.5. Air-Gapped & Offline Isolation
Cadran is 100% self-contained:
- All stylesheets, icons, and fonts (Inter, Bootstrap Icons) are embedded in local wwwroot bundles.
- No Content Delivery Networks (CDNs) or external asset URLs are referenced.
- Operates flawlessly in completely disconnected network environments, private homelabs, or air-gapped workstations.

---

## 4. Key Concepts & Financial Rationale

### 4.1. Real Date vs. Budget Imputation Date
A common budgeting issue occurs when end-of-month paychecks (e.g., deposited on April 28th) distort April cashflow instead of funding May expenses.
- **Real Date**: The physical date the bank processed the transaction (used for statement reconciliations).
- **Budget Imputation Date**: The accounting period the transaction finances. Rules can automatically shift late deposits to `+1 month`, keeping your monthly budget comparisons accurate.

### 4.2. Safe-to-Spend Calculation
$$\text{Safe-to-Spend} = \text{Current Checking Balance} - \sum \text{Pending Recurring Obligations until Next Paycheck}$$

Rather than waiting for scheduled bills to clear, Cadran reserves funds for anticipated recurring debits from day 1 of the billing cycle.

### 4.3. Security Model & Remote Access (VPN vs. Untested TOTP)
Cadran operates on a two-tier defense model (user identity partitioning and optional SQLite database-at-rest encryption with SQLCipher).

> [!WARNING]
> **Important Security Notice on Remote Access & TOTP 2FA**:
> Time-based One-Time Password (TOTP) two-factor authentication was initially implemented in the codebase with offline recovery keys. However, **this feature is untested and will be deprecated and removed in a future release**.
> Exposing Cadran directly to the public internet is strongly discouraged. Cadran is strictly designed as a local-first application. If you wish to access your Cadran instance remotely from outside your local network, you **must not** expose the web server port directly to the internet; use a secure private VPN tunnel such as **WireGuard** or Tailscale instead.

---

## 5. Functional Reference

### 5.1. Pattern Matching Syntax in Rules
| Pattern | Syntax | Example | Matches |
|---|---|---|---|
| **Exact** | Plain text | `NETFLIX` | Exact label `NETFLIX` |
| **Wildcard prefix/suffix** | `*` or `%` | `*AMAZON*` | Any transaction containing `AMAZON` |
| **Single character** | `?` | `TAXI ?` | `TAXI 1`, `TAXI A` |
| **Regular expression** | `^regex$` | `^(UBER\|BOLT)\s*TRIP` | Labels starting with `UBER TRIP` or `BOLT TRIP` |

### 5.2. Supported Import Formats
- **OFX 1.x / 2.x & QFX**: Full support for `<STMTTRN>`, `<FITID>`, `<TRNAMT>`, `<NAME>`, `<MEMO>`.
- **CSV**: Flexible column mapping profiles supporting custom delimiters (`;`, `,`, `\t`), date formats (`dd/MM/yyyy`, `yyyy-MM-dd`, `MM/dd/yyyy`), and decimal notations (`.` or `,`).
