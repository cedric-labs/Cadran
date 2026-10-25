# Guide Utilisateur : Ventiler une Transaction Multi-Catégories

[← Précédent : 13 - Modifier une Transaction](13-edit-transaction.md) | [← Retour à l'Index Général](../README.md) | [Suivant : 15 - Rapprochements Groupés & Actions en Masse →](15-batch-actions.md)

---

Le **Gestionnaire de Ventilation** vous permet de décomposer un ticket de caisse ou un paiement unique entre plusieurs catégories et marchands distincts (par exemple, un passage en hypermarché couvrant à la fois l'Alimentation et l'Entretien de la maison).

---

## 1. Vue d'Ensemble & Repères Visuels

![Ventilation d'opération multi-catégories](../../images/screens/14-split-transaction-screen-annotated.png)

---

## 2. Tableau de Référence des Actions

| Repère | Élément d'Interface | Type d'Action | Consigne |
| :---: | :--- | :---: | :--- |
| <kbd>1</kbd> | **Montant total de l'opération** | **LECTURE SEULE** | Indique le montant bancaire total débité qui doit être intégralement ventilé. |
| <kbd>2</kbd> | **Solde restant à ventiler** | **INDICATEUR D'ÉTAT** | Affiche le solde non affecté. Il devient vert et affiche `0,00 €` dès que la répartition est parfaite. |
| <kbd>3</kbd> | **Montant de la ligne ventilée** | **SAISIR LE MONTANT** | Saisissez la fraction de la dépense correspondant à cette part du ticket. |
| <kbd>4</kbd> | **Catégorie de la part** | **CHOISIR L'OPTION** | Sélectionnez la catégorie budgétaire spécifique attribuée à cette ligne. |
| <kbd>5</kbd> | **Bouton "Ajouter une ligne"** | **CLIQUER ICI** | Insérez une ligne supplémentaire pour diviser la facture entre plus de catégories. |
| <kbd>6</kbd> | **Bouton "Enregistrer"** | **CLIQUER ICI** | Validez la décomposition. Le bouton n'est actif que si le reste à ventiler est strictement égal à 0,00 €. |

---

## 3. Rigueur Comptable & Invariant de Somme Nulle

Cadran garantit une rigueur comptable absolue :
- Il est impossible d'enregistrer une ventilation si un solde résiduel subsiste.
- La somme de toutes les lignes filles doit égaler exactement le montant de l'opération bancaire parente.
- Dans les graphiques budgétaires et rapports, l'opération parente s'efface au profit de ses lignes ventilées pour refléter fidèlement vos habitudes réelles de consommation.

---

## 4. Instructions Pas à Pas

1. **Ouvrir le module de ventilation** : Cliquez sur l'icône de séparation de la transaction.
2. **Définir la première part** : Indiquez le montant (<kbd>3</kbd>) et la catégorie correspondante (<kbd>4</kbd>).
3. **Ajouter les postes suivants** : Cliquez sur **Ajouter une ligne** (<kbd>5</kbd>) pour renseigner les autres articles.
4. **Vérifier l'équilibre** : Assurez-vous que l'indicateur (<kbd>2</kbd>) indique bien `0,00 €` sur fond vert.
5. **Enregistrer** : Cliquez sur **Enregistrer** (<kbd>6</kbd>) pour sceller la ventilation.
