# Guide Utilisateur : Échéancier des Charges Récurrentes & Reste à Vivre

[← Précédent : 18 - Moteur de Règles Automatiques](18-rules-engine.md) | [← Retour à l'Index Général](../README.md) | [Suivant : 20 - Import de Relevés (OFX / CSV) →](20-statement-imports.md)

---

L'**Échéancier des Dépenses Récurrentes** assure le suivi de vos charges incompressibles mensuelles (loyers, abonnements, assurances, factures d'énergie, remboursements de prêts). Il affiche la progression de vos prélèvements déjà débités par rapport à ceux restant à venir et calcule en direct votre véritable **Reste à Vivre**.

---

## 1. Vue d'Ensemble & Repères Visuels

![Échéancier récurrent et reste à vivre](../../images/screens/19-recurring-transactions-screen-annotated.png)

---

## 2. Tableau de Référence des Actions

| Repère | Élément d'Interface | Type d'Action | Consigne |
| :---: | :--- | :---: | :--- |
| <kbd>1</kbd> | **Navigation par mois** | **CLIQUER SUR LES FLÈCHES** | Basculez entre les mois passés, le mois en cours et les projections du mois suivant. |
| <kbd>2</kbd> | **Bouton "Nouvelle charge"** | **CLIQUER ICI** | Ouvrez le formulaire pour planifier un abonnement, un loyer ou une dépense récurrente. |
| <kbd>3</kbd> | **Carte "Total Prévu"** | **SYNTHÈSE BUDGÉTAIRE** | Somme budgétée de l'ensemble de vos engagements récurrents sur le mois sélectionné. |
| <kbd>4</kbd> | **Carte "Total Débité & Progression"** | **SUIVI EN DIRECT** | Montant cumulé et jauge de progression des charges déjà débitées par la banque. |
| <kbd>5</kbd> | **Carte "Total En Attente"** | **SYNTHÈSE BUDGÉTAIRE** | Montant résiduel des prélèvements qui doivent encore intervenir d'ici la fin du mois. |
| <kbd>6</kbd> | **Carte "Reste à Vivre"** | **BUDGET DISPONIBLE** | Solde disponible après déduction de la totalité de vos charges fixes sur vos revenus. |
| <kbd>7</kbd> | **Ligne de charge récurrente** | **CONSULTER / ÉDITER** | Détail de la charge (nom, montant attendu, date habituelle, statut et actions d'édition). |

---

## 3. Rapprochement Automatique & Détection des Factures

- **Rapprochement automatique** : Chaque charge récurrente contient un motif de recherche (ex. `EDF`, `IMMOBILIER PARIS`). Dès l'import d'un relevé, Cadran associe automatiquement le débit et bascule son état de *En attente* à *Débité*.
- **Détecteur de candidats** : Cadran analyse l'historique de vos comptes pour repérer automatiquement vos récurrences et vous proposer de les ajouter à votre échéancier en un seul clic.

---

## 4. Instructions Pas à Pas

1. **Vérifier la situation du mois** : Observez les cartes d'indicateurs (<kbd>3</kbd>, <kbd>4</kbd>, <kbd>5</kbd>) pour jauger vos prélèvements passés et à venir.
2. **Ajouter un nouvel abonnement** : Cliquez sur **Nouvelle charge** (<kbd>2</kbd>), précisez le jour habituel du prélèvement, le montant et la catégorie.
3. **Changer de période** : Utilisez les flèches du calendrier (<kbd>1</kbd>) pour anticiper le budget des mois à venir.
4. **Piloter vos dépenses discrétionnaires** : Consultez le **Reste à Vivre** (<kbd>6</kbd>) pour connaître votre capacité d'épargne et de loisirs en toute sécurité.
