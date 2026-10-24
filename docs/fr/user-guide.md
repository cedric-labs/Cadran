# Cadran - Guide Utilisateur & Exploitation en Production

<p align="center">
  <strong>Langues :</strong>
  <a href="../user-guide.md">English</a> |
  <strong>Français</strong> |
  <a href="../es/user-guide.md">Español</a> |
  <a href="../de/user-guide.md">Deutsch</a>
</p>

Bienvenue dans le manuel d'utilisation officiel de **Cadran**, une plateforme moderne, locale et souveraine dédiée aux finances personnelles, au suivi budgétaire et à la gestion de patrimoine.

Ce guide est structuré selon le framework documentaire **Diátaxis** :
- 🎓 **[1. Tutoriel de Prise en Main](#1-tutoriel-de-prise-en-main)** *(Apprentissage)* : Un parcours pas à pas pour configurer votre espace, importer votre premier relevé et inspecter votre santé financière en 10 minutes.
- 🛠️ **[2. Guides Pratiques & Recettes](#2-guides-pratiques--recettes)** *(Orienté tâches)* : Procédures détaillées pour les opérations courantes (profils d'import CSV, ventilation de tickets, règles d'automatisation, sauvegardes chiffrées).
- 🚀 **[3. Déploiement & Exploitation en Production](#3-d%C3%A9ploiement--exploitation-en-production)** *(Opérationnel)* : Superviseur Windows Tray, exécution en service Windows avec NSSM, binaire autonome Linux, archive de distribution avec unité systemd durcie, et environnements air-gapped.
- 💡 **[4. Concepts Clés & Logique Financière](#4-concepts-cl%C3%A9s--logique-financi%C3%A8re)** *(Compréhension)* : Principes comptables, Date Réelle vs Date d'Imputation Budgétaire, calcul du Reste à Vivre, et modèle de sécurité à deux niveaux.
- 📖 **[5. Référence Fonctionnelle](#5-r%C3%A9f%C3%A9rence-fonctionnelle)** *(Information)* : Syntaxe des motifs et jokers, formats d'import et navigation au clavier.

---

## 1. Tutoriel de Prise en Main

### 1.1. Démarrer l'Application
1. **Lancer le binaire** :
   - Sous Windows : Double-cliquez sur `Cadran.Launcher.exe` (ou exécutez `dotnet run --project Ledger/Ledger.csproj`).
   - Sous Linux : Exécutez `./Cadran-1.0.0-linux-x64` ou démarrez le service systemd `cadran.service`.
2. **Accéder à l'Interface Web** : Ouvrez votre navigateur sur `http://localhost:5000`.
3. **Sélectionner la Langue** : Cliquez sur le sélecteur de langue (`EN` / `FR`) dans la barre de navigation.
4. **Créer Votre Compte** :
   - Cliquez sur **Créer un compte**.
   - Renseignez votre nom complet, adresse e-mail et un mot de passe sécurisé (au moins 8 caractères, mêlant lettres et chiffres).
   - Lors de la création, Cadran initialise automatiquement un catalogue complet de catégories de revenus et de dépenses par défaut.

### 1.2. Créer Votre Premier Compte Bancaire
1. Dans la barre de navigation, rendez-vous dans **Comptes** (`/Accounts`).
2. Cliquez sur **Nouveau Compte**.
3. Renseignez l'établissement bancaire (ex. `BoursoBank`, `BNP Paribas`, `Crédit Agricole`, `CIC`).
4. Définissez le **Nom du compte** (ex. `Compte Courant Principal`).
5. Choisissez le **Type de compte** (Courant, Épargne, Investissement, Carte de crédit) et la **Devise** (ex. `EUR`, `USD`, `GBP`, `CHF`).
6. Saisissez votre **Solde initial** avant le premier relevé importé, puis cliquez sur **Enregistrer**.

### 1.3. Importer Votre Premier Relevé
1. Rendez-vous dans **Imports** (`/Import`).
2. Sélectionnez le compte cible dans la liste déroulante.
3. Choisissez le format de votre fichier :
   - **OFX / QFX** : Format bancaire natif avec validation automatique du numéro de compte `<ACCTID>`.
   - **CSV** : Fichier texte délimité avec profil de correspondance configurable.
4. Glissez-déposez votre relevé bancaire.
5. Contrôlez l'aperçu des écritures et cliquez sur **Confirmer l'import**. Cadran filtre automatiquement les éventuels doublons.

---

## 2. Guides Pratiques & Recettes

### 2.1. Ventiler une Transaction sur Plusieurs Catégories
Lorsqu'un ticket de caisse comporte des achats relevant de postes budgétaires distincts (ex. courses alimentaires et fournitures de bureau) :
1. Rendez-vous dans **Transactions** (`/Transactions`).
2. Cliquez sur la ligne de la transaction pour ouvrir le panneau d'édition.
3. Cliquez sur **Ventiler la transaction**.
4. Spécifiez pour chaque ligne le montant, la catégorie et le sous-tiers facultatif.
5. La somme des lignes de ventilation doit égaler exactement le montant total de la transaction d'origine.
6. Cliquez sur **Enregistrer la ventilation**.

### 2.2. Automatiser la Catégorisation avec le Moteur de Règles
1. Accédez à **Paramètres > Règles d'automatisation** (`/Rules`).
2. Cliquez sur **Nouvelle règle**.
3. Définissez les critères de correspondance :
   - **Motif du libellé** : Utilisez des jokers (`*AMZN*`, `NETFLIX%`) ou des expressions régulières (`^UBER\s*EATS`).
   - **Filtre de compte** : Ciblez un compte précis ou appliquez la règle globalement.
   - **Plage de montants** : Bornes minimale et maximale optionnelles.
4. Définissez l'action cible :
   - Assigner la catégorie et la sous-catégorie.
   - Assigner le tiers / commerçant normalisé.
   - Optionnel : Activer **Décaler au mois budgétaire suivant** (décalage de +1 mois).
5. Cochez **Appliquer rétroactivement aux écritures existantes** pour retraiter l'historique.
6. Cliquez sur **Enregistrer la règle**.

### 2.3. Gérer les Charges Récurrentes & le Reste à Vivre
1. Rendez-vous dans **Charges Récurrentes** (`/Recurring`).
2. Examinez les abonnements et factures périodiques détectés automatiquement (abonnements, loyer, énergie, forfaits).
3. Confirmez ou ajustez la fréquence de prélèvement (Mensuelle, Trimestrielle, Annuelle) et le jour prévu.
4. L'indicateur **Reste à Vivre** (*Safe-to-Spend*) déduit dynamiquement de votre solde courant les engagements récurrents à venir jusqu'au prochain revenu prévu.

### 2.4. Générer des Tableaux Croisés Dynamiques (TCD)
1. Rendez-vous dans **Rapports > Tableau Croisé Dynamique** (`/Reports/Pivot`).
2. Sélectionnez vos axes :
   - **Axe des Lignes** : Catégories, Tiers ou Comptes.
   - **Axe des Colonnes** : Mois, Trimestre ou Année.
   - **Valeurs** : Total Net, Entrées, Sorties ou Nombre d'écritures.
3. Cliquez sur n'importe quelle cellule pour explorer les transactions sous-jacentes dans une fenêtre modale d'inspection.
4. Exportez l'agrégation résultante au format CSV à tout moment.

---

## 3. Déploiement & Exploitation en Production

### 3.1. Superviseur Windows Tray (`Cadran.Launcher.exe`)
Pour les environnements de bureau Windows, `Cadran.Launcher.exe` fournit une intégration discrète :
- Se loge dans la zone de notification Windows (près de l'horloge).
- Démarre automatiquement le serveur Kestrel et ouvre votre navigateur par défaut.
- Exécute des sauvegardes compressées et horodatées de la base de données au lancement et à la fermeture.
- Propose un menu contextuel en un clic : **Afficher les journaux** et **Redémarrer le serveur**.

### 3.2. Exécution en Service Windows Durci (NSSM)
Pour exécuter Cadran en arrière-plan permanent sans nécessiter l'ouverture d'une session utilisateur interactive, utilisez **NSSM** (Non-Sucking Service Manager) :

1. **Installer NSSM** :
   ```powershell
   # Via winget
   winget install NSSM.NSSM

   # Via Chocolatey
   choco install nssm

   # Via Scoop
   scoop install nssm
   ```

2. **Configurer et Installer le Service** :
   ```powershell
   # Définir les chemins
   $serviceName = "Cadran"
   $binaryPath = "C:\Cadran\app\Cadran.exe"
   $appDir = "C:\Cadran\app"

   # Installer le service
   nssm install $serviceName $binaryPath
   nssm set $serviceName AppDirectory $appDir
   nssm set $serviceName AppParameters "--urls http://0.0.0.0:5000"
   nssm set $serviceName Start SERVICE_AUTO_START

   # Définir les variables d'environnement
   nssm set $serviceName AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production" "Server__AllowNetworkAccess=true"

   # Démarrer le service
   nssm start $serviceName
   ```

3. **Vérifier l'État du Service** :
   ```powershell
   Get-Service -Name Cadran
   ```

### 3.3. Déploiement Linux : Binaire Autonome vs Archive de Déploiement

Cadran propose deux formats de distribution Linux x64 distincts, adaptés à des besoins opérationnels précis. Les deux packages sont 100 % autonomes (self-contained) et intègrent le runtime .NET 9 ainsi que le moteur SQLCipher, sans aucune dépendance préalable requise sur le système hôte.

#### Comparatif des Formats

| Critère | Exécutable Autonome (`Cadran-*-linux-x64`) | Archive de Déploiement (`Cadran-*-linux-x64.tar.gz`) |
|---|---|---|
| **Packaging** | Binaire ELF unique et autonome | Archive tarball compressée |
| **Extraction Requise** | Non (Exécution directe) | Oui (`tar -xzf ...`) |
| **Structure Fournie** | Exécutable seul (crée `data/` automatiquement) | Arborescence complète (`data/`, `backups/`, `README.md`, `LICENSE`) |
| **Cas d'Usage Recommandé** | Évaluations rapides, postes de dév, scripts | Serveurs de production, serveurs familiaux, services systemd |
| **Gestion du Disque** | Création automatique dans le répertoire courant | Droits et répertoires préconfigurés dans `/opt/cadran` |

---

#### Méthode A : Binaire Autonome Unique (`Cadran-*-linux-x64`)

**Quand le choisir :** Vous souhaitez lancer Cadran sous Linux le plus rapidement possible, sans décompression ni configuration préalable.

1. **Télécharger et accorder les droits d'exécution** :
   ```bash
   chmod +x Cadran-1.0.0-linux-x64
   ```

2. **Lancer directement** :
   ```bash
   ./Cadran-1.0.0-linux-x64 --urls http://0.0.0.0:5000
   ```
   *Remarque :* Au premier démarrage, Cadran détecte l'absence du répertoire de données, crée automatiquement `./data/` et initialise `cadran.db`.

---

#### Méthode B : Archive de Déploiement en Production (`Cadran-*-linux-x64.tar.gz`) avec `systemd`

**Quand le choisir :** Vous déployez Cadran comme service d'arrière-plan permanent sur un serveur domestique ou un VPS avec démarrage automatique au boot et supervision du processus.

1. **Créer un utilisateur système dédié sans privilèges et l'arborescence** :
   ```bash
   sudo useradd --system --shell /usr/sbin/nologin --home-dir /opt/cadran cadran
   sudo mkdir -p /opt/cadran
   ```

2. **Extraire l'archive et ajuster les permissions** :
   ```bash
   sudo tar -xzf Cadran-1.0.0-linux-x64.tar.gz -C /opt/cadran
   sudo chown -R cadran:cadran /opt/cadran
   sudo chmod +x /opt/cadran/Cadran
   ```

3. **Déployer l'unité de service systemd durcie (`/etc/systemd/system/cadran.service`)** :
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

   # Durcissement de sécurité
   NoNewPrivileges=true
   ProtectSystem=full
   ProtectHome=true
   PrivateTmp=true
   ProtectControlGroups=true

   # Environnement
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

   [Install]
   WantedBy=multi-user.target
   ```

4. **Activer et démarrer le service** :
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable --now cadran.service
   sudo systemctl status cadran.service
   ```

5. **Consulter les journaux d'application** :
   ```bash
   journalctl -u cadran.service -f
   ```

### 3.4. Fonctionnement Hors-Ligne & Air-Gapped
Cadran est 100 % autonome :
- L'ensemble des feuilles de style, icônes et polices (Inter, Bootstrap Icons) est hébergé localement dans `wwwroot`.
- Aucun CDN ni URL d'actif externe n'est sollicité.
- Fonctionne parfaitement dans des réseaux isolés, des homelabs privés ou des postes sécurisés déconnectés d'Internet.

---

## 4. Concepts Clés & Logique Financière

### 4.1. Date Réelle vs Date d'Imputation Budgétaire
Un problème récurrent en gestion budgétaire survient lorsque les salaires de fin de mois (ex. virés le 28 avril) faussent la trésorerie d'avril au lieu de financer les charges du mois de mai.
- **Date Réelle** : Date bancaire effective de l'opération (utilisée pour le pointage et le rapprochement).
- **Date d'Imputation Budgétaire** : Période comptable que l'écriture finance réellement. Des règles peuvent décaler automatiquement les salaires tardifs à `+1 mois` pour conserver des comparaisons budgétaires mensuelles exactes.

### 4.2. Calcul du Reste à Vivre
$$\text{Reste à Vivre} = \text{Solde Courant du Compte} - \sum \text{Charges Récurrentes en Attente jusqu'au Prochain Salaire}$$

Plutôt que d'attendre que les factures planifiées soient prélevées, Cadran provisionne dès le début du cycle budgétaire les engagements périodiques attendus.

### 4.3. Modèle de Sécurité & Accès Distant (VPN vs 2FA TOTP Non Testée)
Cadran s'appuie sur une défense à deux niveaux (cloisonnement de l'identité utilisateur et chiffrement optionnel au repos de la base SQLite avec SQLCipher).

> [!WARNING]
> **Avertissement de Sécurité Critique concernant l'Accès Distant & la 2FA TOTP** :
> L'authentification à deux facteurs TOTP par mot de passe à usage unique a été initialement implémentée dans le code avec des clés de secours hors-ligne. Toutefois, **cette fonctionnalité n'est pas testée et est programmée pour dépréciation et suppression dans une version future**.
> Il est formellement déconseillé d'exposer Cadran directement sur l'Internet public. Cadran est strictement conçu comme une application locale. Si vous souhaitez accéder à distance à votre instance Cadran en dehors de votre réseau local, vous **ne devez pas** ouvrir directement le port web sur votre routeur ; utilisez impérativement un tunnel VPN privé sécurisé tel que **WireGuard** ou Tailscale.

---

## 5. Référence Fonctionnelle

### 5.1. Syntaxe des Motifs dans les Règles
| Motif | Syntaxe | Exemple | Correspondance |
|---|---|---|---|
| **Exact** | Texte brut | `NETFLIX` | Libellé exact `NETFLIX` |
| **Jokers préfixe/suffixe** | `*` ou `%` | `*AMAZON*` | Toute écriture contenant `AMAZON` |
| **Caractère unique** | `?` | `TAXI ?` | `TAXI 1`, `TAXI A` |
| **Expression régulière** | `^regex$` | `^(UBER\|BOLT)\s*COURSE` | Libellés commençant par `UBER COURSE` ou `BOLT COURSE` |

### 5.2. Formats d'Import Pris en Charge
- **OFX 1.x / 2.x & QFX** : Prise en charge intégrale des balises `<STMTTRN>`, `<FITID>`, `<TRNAMT>`, `<NAME>`, `<MEMO>`.
- **CSV** : Profils de mappage flexibles acceptant les délimiteurs personnalisés (`;`, `,`, `\t`), les formats de dates (`dd/MM/yyyy`, `yyyy-MM-dd`, `MM/dd/yyyy`) et les notations décimales (`.` ou `,`).
