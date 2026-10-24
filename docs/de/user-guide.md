# Cadran - Benutzerhandbuch & Produktionsbetrieb

<p align="center">
  <strong>Sprachen:</strong>
  <a href="../user-guide.md">English</a> |
  <a href="../fr/user-guide.md">Français</a> |
  <a href="../es/user-guide.md">Español</a> |
  <strong>Deutsch</strong>
</p>

Willkommen im offiziellen Benutzerhandbuch für **Cadran**, eine moderne, datenschutzorientierte Plattform für persönliche Finanzen, Budgetierung und Vermögensverwaltung mit lokalem Datenfokus.

Dieses Handbuch ist nach dem **Diátaxis**-Framework strukturiert:
- 🎓 **[1. Schnellstart-Tutorial](#1-schnellstart-tutorial)** *(Lernorientiert)*: Schritt für Schritt Ihren Arbeitsbereich einrichten, den ersten Auszug importieren und Ihre Finanzlage in 10 Minuten überblicken.
- 🛠️ **[2. Praktische Anleitungen & Rezepte](#2-praktische-anleitungen--rezepte)** *(Aufgabenorientiert)*: Konkrete Abläufe für den Alltag (CSV-Profil-Erstellung, Belegaufteilung, Automatisierungsregeln, verschlüsselte Sicherungen).
- 🚀 **[3. Bereitstellung & Produktionsbetrieb](#3-bereitstellung--produktionsbetrieb)** *(Betriebsorientiert)*: Windows-Tray-Supervisor, Betrieb als Windows-Dienst mit NSSM, Linux-Einzeldatei und Distributionsarchiv mit systemd-Absicherung sowie Offline-Betrieb (Air-Gap).
- 💡 **[4. Kernkonzepte & Finanzlogik](#4-kernkonzepte--finanzlogik)** *(Verständnisorientiert)*: Buchungsprinzipien, Reales Datum vs. Budgetzuordnungsdatum, Berechnung des verfügbaren Budgets und zweistufiges Sicherheitsmodell.
- 📖 **[5. Funktionsreferenz](#5-funktionsreferenz)** *(Informationsorientiert)*: Platzhaltersyntax, Importformate und Tastaturbedienung.

---

## 1. Schnellstart-Tutorial

### 1.1. Anwendung starten
1. **Programm starten**:
   - Unter Windows: Doppelklick auf `Cadran.Launcher.exe` (oder `dotnet run --project Ledger/Ledger.csproj` im Terminal ausführen).
   - Unter Linux: `./Cadran-1.0.0-linux-x64` ausführen oder den systemd-Dienst `cadran.service` starten.
2. **Weboberfläche öffnen**: Öffnen Sie Ihren Browser unter `http://localhost:5000`.
3. **Sprache auswählen**: Klicken Sie auf den Sprachumschalter (`EN` / `FR`) in der oberen Navigationsleiste.
4. **Benutzerkonto registrieren**:
   - Klicken Sie auf **Konto erstellen**.
   - Geben Sie Ihren Namen, Ihre E-Mail-Adresse und ein sicheres Passwort ein (mindestens 8 Zeichen mit Buchstaben und Zahlen).
   - Beim Erstellen initialisiert Cadran automatisch einen Standardkatalog für Einnahmen- und Ausgabenkategorien.

### 1.2. Erstes Bankkonto anlegen
1. Wechseln Sie in der Navigationsleiste auf **Konten** (`/Accounts`).
2. Klicken Sie auf **Neues Konto**.
3. Geben Sie Ihr Kreditinstitut an (z. B. `Deutsche Bank`, `ING`, `Commerzbank`, `Sparkasse`).
4. Vergeben Sie einen **Kontonamen** (z. B. `Hauptgirokonto`).
5. Wählen Sie den **Kontotyp** (Girokonto, Sparkonto, Depot, Kreditkarte) und die **Währung** (z. B. `EUR`, `USD`, `CHF`, `GBP`).
6. Tragen Sie Ihren **Eröffnungssaldo** vor dem ersten zu importierenden Kontoauszug ein und klicken Sie auf **Speichern**.

### 1.3. Ersten Kontoauszug importieren
1. Navigieren Sie zu **Import** (`/Import`).
2. Wählen Sie das Zielkonto im Dropdown-Menü aus.
3. Wählen Sie Ihr Dateiformat:
   - **OFX / QFX**: Natives Bankformat mit automatischer `<ACCTID>`-Validierung.
   - **CSV**: Flexibles getrenntes Textformat mit anpassbaren Profilen.
4. Ziehen Sie Ihre Bankdatei per Drag & Drop in den Upload-Bereich.
5. Prüfen Sie die Vorschau der Buchungen und klicken Sie auf **Import bestätigen**. Cadran sortiert Duplikate automatisch aus.

---

## 2. Praktische Anleitungen & Rezepte

### 2.1. Transaktion auf mehrere Kategorien aufteilen (Splitting)
Wenn ein Kassenbon Artikel unterschiedlicher Budgetbereiche umfasst (z. B. Lebensmittel und Bürobedarf):
1. Öffnen Sie **Buchungen** (`/Transactions`).
2. Klicken Sie auf die Transaktionszeile, um das Bearbeitungsfenster zu öffnen.
3. Wählen Sie **Transaktion aufteilen**.
4. Tragen Sie für jeden Teilbetrag die gewünschte Kategorie und optional einen Unterhändler ein.
5. Die Summe der Teilbeträge muss exakt dem ursprünglichen Gesamtbetrag entsprechen.
6. Klicken Sie auf **Aufteilung speichern**.

### 2.2. Kategorisierung mit der Regel-Engine automatisieren
1. Gehen Sie zu **Einstellungen > Automatisierungsregeln** (`/Rules`).
2. Klicken Sie auf **Neue Regel**.
3. Definieren Sie die Bedingungen:
   - **Muster für Buchungstext**: Platzhalter (`*AMZN*`, `NETFLIX%`) oder reguläre Ausdrücke (`^UBER\s*EATS`) verwenden.
   - **Kontofilter**: Auf ein bestimmtes Konto beschränken oder global anwenden.
   - **Betragsbereich**: Optionale Mindest- und Höchstgrenzen festlegen.
4. Definieren Sie die Folgeaktion:
   - Kategorie und Unterkategorie zuweisen.
   - Einheitlichen Zahlungsempfänger zuweisen.
   - Optional: **Auf nächsten Budgetmonat verschieben** aktivieren (+1 Monat Verschiebung).
5. Aktivieren Sie **Rückwirkend auf bestehende Buchungen anwenden**, um frühere Auszüge nachzupflegen.
6. Klicken Sie auf **Regel speichern**.

### 2.3. Wiederkehrende Ausgaben & Verfügbares Budget verwalten
1. Öffnen Sie **Wiederkehrende Ausgaben** (`/Recurring`).
2. Überprüfen Sie die automatisch erkannten Abonnements und Daueraufträge (Fitnessstudio, Streaming, Miete, Strom).
3. Bestätigen oder korrigieren Sie die Abbuchungsfrequenz (Monatlich, Quartalsweise, Jährlich) und den Fälligkeitstag.
4. Das **Verfügbare Budget** (*Safe-to-Spend*) zieht ausstehende Fixkosten im Laufe des Monats dynamisch von Ihrem aktuellen Saldo ab.

### 2.4. Dynamische Pivot-Tabellen (TCD) erstellen
1. Wechseln Sie zu **Berichte > Pivot-Tabelle** (`/Reports/Pivot`).
2. Wählen Sie Ihre Dimensionen:
   - **Zeilenachse**: Kategorien, Zahlungsempfänger oder Konten.
   - **Spaltenachse**: Monat, Quartal oder Jahr.
   - **Werte**: Nettosumme, Einnahmen, Ausgaben oder Transaktionsanzahl.
3. Klicken Sie auf eine beliebige Zelle, um die zugrunde liegenden Einzelbuchungen im Detailmodal zu prüfen.
4. Exportieren Sie die Auswertung jederzeit nach CSV.

---

## 3. Bereitstellung & Produktionsbetrieb

### 3.1. Windows-Tray-Supervisor (`Cadran.Launcher.exe`)
Auf Windows-Arbeitsplätzen bietet `Cadran.Launcher.exe` eine komfortable System-Tray-Integration:
- Läuft unaufdringlich im Infobereich neben der Uhr.
- Startet den Kestrel-Server und öffnet Ihren Standardbrowser automatisch.
- Führt beim Programmstart und beim Beenden zeitgestempelte Datenbank-Backups durch.
- Bietet ein Schnellmenü für **Protokolle anzeigen** und **Server neu starten**.

### 3.2. Betrieb als Windows-Dienst mit NSSM
Um Cadran dauerhaft im Hintergrund ohne aktive Benutzeranmeldung zu betreiben, empfiehlt sich **NSSM** (Non-Sucking Service Manager):

1. **NSSM installieren**:
   ```powershell
   # Über winget
   winget install NSSM.NSSM

   # Über Chocolatey
   choco install nssm

   # Über Scoop
   scoop install nssm
   ```

2. **Dienst einrichten und starten**:
   ```powershell
   # Pfade definieren
   $serviceName = "Cadran"
   $binaryPath = "C:\Cadran\app\Cadran.exe"
   $appDir = "C:\Cadran\app"

   # Dienst anlegen
   nssm install $serviceName $binaryPath
   nssm set $serviceName AppDirectory $appDir
   nssm set $serviceName AppParameters "--urls http://0.0.0.0:5000"
   nssm set $serviceName Start SERVICE_AUTO_START

   # Umgebungsvariablen setzen
   nssm set $serviceName AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production" "Server__AllowNetworkAccess=true"

   # Dienst starten
   nssm start $serviceName
   ```

3. **Status prüfen**:
   ```powershell
   Get-Service -Name Cadran
   ```

### 3.3. Linux-Betrieb: Standalone-Binary vs. Distributionsarchiv

Cadran wird für Linux x64 in zwei Varianten angeboten. Beide sind vollständig in sich geschlossen (self-contained) und bündeln die .NET 9-Laufzeitumgebung sowie die SQLCipher-Engine, sodass keine externen Systembibliotheken vorinstalliert werden müssen.

#### Formatvergleich

| Kriterium | Standalone-Binary (`Cadran-*-linux-x64`) | Distributionsarchiv (`Cadran-*-linux-x64.tar.gz`) |
|---|---|---|
| **Paketierung** | Einzelne eigenständige ELF-Binary | Komprimiertes Tarball-Archiv |
| **Entpacken erforderlich** | Nein (Direkter Start) | Ja (`tar -xzf ...`) |
| **Enthaltene Ordnerstruktur** | Nur Binary (erstellt `data/` automatisch) | Vollständiges Layout (`data/`, `backups/`, `README.md`, `LICENSE`) |
| **Empfohlen für** | Schnelle Tests, portable Nutzung, Entwicklungsrechner | Produktions- und Heimserver, systemd-Dienste |
| **Datenträgerverwaltung** | Automatische Ordnererstellung im aktuellen Pfad | Vorkonfigurierte Rechte im Systemordner `/opt/cadran` |

---

#### Methode A: Standalone-Einzeldatei (`Cadran-*-linux-x64`)

**Wann wählen:** Wenn Sie Cadran unter Linux ohne vorheriges Entpacken sofort ausführen möchten.

1. **Herunterladen und Ausführungsrechte vergeben**:
   ```bash
   chmod +x Cadran-1.0.0-linux-x64
   ```

2. **Direkt starten**:
   ```bash
   ./Cadran-1.0.0-linux-x64 --urls http://0.0.0.0:5000
   ```
   *Hinweis:* Beim Erststart legt Cadran das Verzeichnis `./data/` an und initialisiert `cadran.db`.

---

#### Methode B: Bereitstellungsarchiv (`Cadran-*-linux-x64.tar.gz`) mit `systemd`

**Wann wählen:** Für die dauerhafte Installation als Hintergrunddienst auf einem Heimserver oder VPS mit automatischem Systemstart.

1. **Systembenutzer und Zielverzeichnis anlegen**:
   ```bash
   sudo useradd --system --shell /usr/sbin/nologin --home-dir /opt/cadran cadran
   sudo mkdir -p /opt/cadran
   ```

2. **Archiv entpacken und Berechtigungen anpassen**:
   ```bash
   sudo tar -xzf Cadran-1.0.0-linux-x64.tar.gz -C /opt/cadran
   sudo chown -R cadran:cadran /opt/cadran
   sudo chmod +x /opt/cadran/Cadran
   ```

3. **Gehärtete systemd-Dienstdatei anlegen (`/etc/systemd/system/cadran.service`)**:
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

   # Sicherheitsabsicherung
   NoNewPrivileges=true
   ProtectSystem=full
   ProtectHome=true
   PrivateTmp=true
   ProtectControlGroups=true

   # Umgebung
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

   [Install]
   WantedBy=multi-user.target
   ```

4. **Dienst aktivieren und starten**:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable --now cadran.service
   sudo systemctl status cadran.service
   ```

5. **Anwendungsprotokolle einsehen**:
   ```bash
   journalctl -u cadran.service -f
   ```

### 3.4. Air-Gapped & Offline-Isolation
Cadran arbeitet zu 100 % unabhängig vom Internet:
- Alle Stylesheets, Symbole und Schriftarten (Inter, Bootstrap Icons) sind lokal in `wwwroot` eingebettet.
- Keine Verbindung zu CDNs oder externen Webservern erforderlich.
- Zuverlässiger Betrieb in isolierten Netzwerken, privaten Heimlaboren oder luftspaltgesicherten Arbeitsplätzen.

---

## 4. Kernkonzepte & Finanzlogik

### 4.1. Reales Buchungsdatum vs. Budgetzuordnungsdatum
Ein typisches Problem bei der Budgetplanung: Am Monatsende gutgeschriebene Gehälter (z. B. am 28. April) verzerren den Cashflow des Aprils, obwohl sie die Kosten des Folgemonats Mai decken sollen.
- **Reales Datum**: Das tatsächliche Valutadatum der Bank (relevant für den Kontenabgleich).
- **Budgetzuordnungsdatum**: Der wirtschaftliche Zeitraum, für den die Buchung bestimmt ist. Automatisierungsregeln können späte Gehälter um `+1 Monat` verschieben, um Monatsvergleiche aussagekräftig zu halten.

### 4.2. Berechnung des verfügbaren Budgets (Safe-to-Spend)
$$\text{Verfügbares Budget} = \text{Aktueller Kontostand} - \sum \text{Ausstehende Fixkosten bis zum nächsten Gehalt}$$

Statt zu warten, bis regelmäßige Rechnungen abgebucht werden, reserviert Cadran diese Beträge ab Tag 1 des Budgetzyklus.

### 4.3. Sicherheitsmodell & Fernzugriff (VPN vs. Ungetestetes TOTP)
Cadran setzt auf ein zweistufiges Sicherheitsmodell (Benutzerdatenisolation und optionale SQLite-Verschlüsselung im Ruhezustand via SQLCipher).

> [!WARNING]
> **Kritischer Sicherheitshinweis zu Fernzugriff & TOTP 2FA**:
> Die zeitbasierte Einmalpasswort-Authentifizierung (TOTP) wurde ursprünglich mit Offline-Wiederherstellungsschlüsseln implementiert. Diese Funktion ist jedoch **nicht getestet und wird in einer künftigen Version entfernt**.
> Eine direkte Freigabe von Cadran im offenen Internet wird dringend abgeraten. Cadran ist ausschließlich für den lokalen Betrieb konzipiert. Falls Sie aus der Ferne auf Ihr System zugreifen möchten, leiten Sie den Webserver-Port **nicht** direkt ins Internet weiter, sondern verwenden Sie stets einen sicheren VPN-Tunnel wie **WireGuard** oder Tailscale.

---

## 5. Funktionsreferenz

### 5.1. Syntax für Regelmuster
| Muster | Syntax | Beispiel | Bedeutung |
|---|---|---|---|
| **Exakt** | Normaler Text | `NETFLIX` | Exakter Text `NETFLIX` |
| **Präfix-/Suffix-Platzhalter** | `*` oder `%` | `*AMAZON*` | Jede Buchung, die `AMAZON` enthält |
| **Einzelzeichen** | `?` | `TAXI ?` | `TAXI 1`, `TAXI A` |
| **Regulärer Ausdruck** | `^regex$` | `^(UBER\|BOLT)\s*FAHRT` | Texte, die mit `UBER FAHRT` oder `BOLT FAHRT` beginnen |

### 5.2. Unterstützte Importformate
- **OFX 1.x / 2.x & QFX**: Vollständige Unterstützung für die Tags `<STMTTRN>`, `<FITID>`, `<TRNAMT>`, `<NAME>`, `<MEMO>`.
- **CSV**: Flexible Zuordnungsprofile mit Unterstützung für Trennzeichen (`;`, `,`, `\t`), Datumsformate (`dd/MM/yyyy`, `yyyy-MM-dd`, `MM/dd/yyyy`) und Dezimaltrennzeichen (`.` oder `,`).
