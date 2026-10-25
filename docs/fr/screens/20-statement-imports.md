# Guide Utilisateur : Import de Relevés Bancaires (OFX & CSV)

[← Précédent : 19 - Échéancier des Charges Récurrentes](19-recurring-transactions.md) | [← Retour à l'Index Général](../README.md) | [Suivant : 21 - Créateur de Profil d'Import CSV →](21-import-mapping.md)

---

Le module d'**Import de Relevés Bancaires** vous permet d'intégrer facilement vos historiques d'écritures exportés depuis vos espaces bancaires en ligne, au format standard Open Financial Exchange (`.ofx`, `.qfx`) ou délimité (`.csv`, `.tsv`, `.txt`). Il intègre une protection anti-doublon et déclenche vos règles d'affectation automatique.

---

## 1. Vue d'Ensemble & Repères Visuels

![Import de relevés bancaires](../../images/screens/20-statement-imports-screen-annotated.png)

---

## 2. Tableau de Référence des Actions

| Repère | Élément d'Interface | Type d'Action | Consigne |
| :---: | :--- | :---: | :--- |
| <kbd>1</kbd> | **Lien "Configurer les profils CSV"** | **CLIQUER ICI** | Ouvrez le gestionnaire de profils CSV pour adapter le format aux exports de banques spécifiques. |
| <kbd>2</kbd> | **Compte bancaire de destination** | **CHOISIR LE COMPTE** | Sélectionnez le compte cible (pour les fichiers OFX contenant `<ACCTID>`, Cadran le détecte automatiquement). |
| <kbd>3</kbd> | **Bouton "Choisir un fichier"** | **SÉLECTIONNER LE FICHIER** | Parcourez vos dossiers locaux pour sélectionner votre relevé bancaire `.csv` ou `.ofx`. |
| <kbd>4</kbd> | **Profil de correspondance CSV** | **CHOISIR LE PROFIL** | Sélectionnez le profil de colonnes adapté (ignoré automatiquement lors de l'import de fichiers OFX/QFX). |
| <kbd>5</kbd> | **Bouton "Lancer l'importation"** | **CLIQUER ICI** | Déclenchez l'analyse du fichier, le filtrage des doublons, l'application des règles et l'intégration au livre de comptes. |
| <kbd>6</kbd> | **Journal d'audit des imports récents** | **CONSULTER L'HISTORIQUE** | Consultez l'historique avec dates, volumes importés, doublons écartés et taux d'auto-catégorisation. |

---

## 3. Détection des Doublons & Intégrité Financière

- **Identifiants uniques OFX** : Les fichiers OFX/QFX intègrent des balises `<FITID>` uniques. Cadran garantit qu'une même opération bancaire ne sera jamais injectée deux fois.
- **Empreinte cryptographique CSV** : Pour les fichiers CSV, Cadran génère une empreinte invariante combinant date, montant, libellé brut et compte, écartant avec précision les écritures déjà importées lors d'un relevé antérieur.
- **Exécution immédiate des règles** : Dès l'import des écritures, l'ensemble de vos règles de catégorisation actives sont appliquées, ordonnant vos comptes instantanément.

---

## 4. Instructions Pas à Pas

1. **Sélectionner le compte de destination** : Choisissez votre compte dans la liste déroulante (<kbd>2</kbd>).
2. **Choisir le fichier** : Cliquez sur **Choisir un fichier** (<kbd>3</kbd>) et sélectionnez votre export bancaire.
3. **Sélectionner le profil (si CSV)** : Précisez le profil de votre banque (<kbd>4</kbd>) ou laissez par défaut pour l'OFX.
4. **Lancer le traitement** : Cliquez sur le bouton principal **Lancer l'importation** (<kbd>5</kbd>).
5. **Contrôler le résultat** : Consultez la notification de synthèse et l'entrée d'audit dans le tableau d'historique (<kbd>6</kbd>).
