# Guide Utilisateur : Modifier une Transaction & Détails de Classification

[← Précédent : 12 - Livre de Comptes Unifié](12-transactions-ledger.md) | [← Retour à l'Index Général](../README.md) | [Suivant : 14 - Ventiler une Transaction Multi-Catégories →](14-split-transaction.md)

---

La fenêtre **Modifier une Transaction** permet d'enrichir les métadonnées d'une écriture bancaire, de lui associer un commerçant reconnu, d'ajouter des notes personnelles et de décaler sa prise en compte budgétaire.

---

## 1. Vue d'Ensemble & Repères Visuels

![Formulaire d'édition de transaction](../../images/screens/13-edit-transaction-screen-annotated.png)

---

## 2. Tableau de Référence des Actions

| Repère | Élément d'Interface | Type d'Action | Consigne |
| :---: | :--- | :---: | :--- |
| <kbd>1</kbd> | **Recherche / Saisie du Marchand** | **SAISIR / CHOISIR** | Recherchez un commerçant existant ou saisissez une nouvelle enseigne pour lier cette opération. |
| <kbd>2</kbd> | **Notes personnelles & Mémo** | **SAISIR DU TEXTE** | Ajoutez des notes de contexte, références de facture ou précisions utiles pour votre comptabilité. |
| <kbd>3</kbd> | **Arborescence des catégories** | **DÉPLIER & CHOISIR** | Parcourez l'arbre hiérarchique ou filtrez par mot-clé pour choisir la catégorie adéquate. |
| <kbd>4</kbd> | **Date budgétaire d'imputation** | **CHOISIR LA DATE** | Décalez la date budgétaire si une dépense débitée en fin de mois doit être imputée sur le budget suivant. |
| <kbd>5</kbd> | **Statut personnalisé** | **SÉLECTIONNER** | Choisissez un statut de suivi spécifique parmi vos configurations personnalisées. |
| <kbd>6</kbd> | **Case "Appliquer aux similaires"** | **COCHER LA CASE** | Cochez cette option pour répercuter automatiquement cette catégorie sur toutes les opérations identiques. |
| <kbd>7</kbd> | **Bouton "Enregistrer"** | **CLIQUER ICI** | Enregistrez vos modifications pour mettre à jour instantanément le livre de comptes. |

---

## 3. Propagation & Règle Automatique Immédiate

En cochant la case **Appliquer aux opérations similaires** (<kbd>6</kbd>) :
- Le système détecte l'ensemble des écritures non catégorisées ayant un libellé bancaire analogue.
- La catégorie et le marchand sont rétroactivement et proactivement appliqués, vous évitant de reclasser manuellement vos dépenses récurrentes (abonnements, supermarchés, factures d'énergie).

---

## 4. Instructions Pas à Pas

1. **Ouvrir la fenêtre** : Cliquez sur la catégorie de l'opération dans le tableau de bord ou le grand livre.
2. **Associer le commerçant** : Saisissez le nom du marchand (<kbd>1</kbd>).
3. **Sélectionner la catégorie** : Parcourez l'arborescence (<kbd>3</kbd>) et cliquez sur la sous-catégorie cible.
4. **Préciser notes et date** : Renseignez vos notes (<kbd>2</kbd>) ou modifiez la date d'imputation budgétaire (<kbd>4</kbd>).
5. **Propager la catégorisation** : Cochez la case (<kbd>6</kbd>) pour traiter toutes les occurrences similaires.
6. **Valider** : Cliquez sur le bouton vert **Enregistrer** (<kbd>7</kbd>).
