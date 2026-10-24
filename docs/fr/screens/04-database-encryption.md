# Guide Utilisateur : Chiffrement de la Base de Données

[← Précédent : 03 - Connexion](03-login.md) | [Retour à l'Index Général](../README.md) | [Suivant : 05 - Déverrouillage à Froid →](05-database-unlock.md)

---

L'écran de **Chiffrement de la Base de Données** vous permet de protéger votre fichier de base de données local avec une phrase secrète maîtresse grâce au chiffrement matériellement indépendant AES-256. Cela garantit que personne ne pourra lire vos données financières, même en copiant directement le fichier sur votre disque dur.

---

## 1. Vue d'Ensemble & Repères Visuels

![Écran de chiffrement de base de données](../../images/screens/04-database-encryption-screen-annotated.png)

---

## 2. Tableau de Référence des Actions

| Repère | Élément d'Interface | Type d'Action | Consigne |
| :---: | :--- | :---: | :--- |
| <kbd>1</kbd> | **Phrase Secrète Maîtresse** | **RENSEIGNER** | Choisissez une clé maîtresse robuste (minimum 8 caractères). Ce mot de passe sera exigé pour déverrouiller la base. |
| <kbd>2</kbd> | **Confirmer la Phrase Secrète** | **RENSEIGNER** | Ressaisissez à l'identique la clé maîtresse pour confirmation. |
| <kbd>3</kbd> | **Mémoriser sur cet appareil** | **OPTIONNEL** | Cochez cette case si vous souhaitez que Cadran mémorise la clé sur cet appareil pour éviter de la saisir à chaque démarrage. |
| <kbd>4</kbd> | **Bouton "Activer le chiffrement"** | **CLIQUER ICI** | Cliquez sur ce bouton pour chiffrer immédiatement votre fichier de base de données avec la protection AES-256. |

---

## 3. Procédure Étape par Étape

1. **Choisir votre clé maîtresse** :
   - Tapez votre clé secrète dans **Phrase secrète maîtresse** (**[1]**).
   - Ressaisissez-la dans **Confirmer la phrase secrète** (**[2]**).
2. **Définir vos préférences de démarrage** :
   - Cochez **Mémoriser sur cet appareil** (**[3]**) pour un déverrouillage automatique et fluide sur votre PC personnel.
   - Laissez la case décochée si vous préférez que Cadran sollicite votre phrase secrète maîtresse à chaque lancement.
3. **Activer la protection** :
   - Cliquez sur le bouton bleu **Activer le chiffrement maintenant** (**[4]**).
   - Vos données sont désormais intégralement chiffrées au repos sur le disque.

> [!CAUTION]
> Si vous perdez votre phrase secrète maîtresse et n'avez pas coché "Mémoriser sur cet appareil", **vos données ne pourront jamais être récupérées**. Conservez votre phrase secrète dans un gestionnaire de mots de passe fiable.
