# Recette du MVP SafeVault 1.0

Vérification de l'application par rapport au cahier des charges (*SafeVault — Documentation du projet, MVP 1.0*).

- **Date :** 19/11/2026
- **Environnement :** Windows 11 Pro, .NET 10, build Debug, coffre de test jetable
- **Tests automatiques :** 143 tests xUnit, tous réussis

Légende : ✅ conforme · 🔧 écart trouvé pendant la recette puis corrigé

## 1. Critères de réussite du MVP (§ 3.2)

| Critère | Vérification | Statut |
|---|---|---|
| Création, verrouillage et déverrouillage du coffre fonctionnels et testés | `VaultServiceTests` et parcours manuel | ✅ |
| CRUD complet avec catégories, favoris et recherche instantanée | `CredentialServiceTests` et parcours manuel | ✅ |
| Aucune donnée sensible en clair sur le disque | `StorageSecurityTests`. Lecture brute de `vault.db` : aucun nom de service, identifiant, mot de passe ni Master Password n'y figure | ✅ |
| Sauvegarde chiffrée exportable et restaurable avec vérification d'intégrité | `BackupProtectorTests`, `BackupServiceTests` et parcours manuel | ✅ |
| Installateur Windows, documentation et dépôt GitHub publiés | Paquet MSIX signé et version portable (`build/publish.ps1`), README, SECURITY.md, PRIVACY.md, licence MIT | ✅ |

## 2. Fonctionnalités (§ 4)

| Réf. | Exigence | Statut |
|---|---|---|
| F1 | Création du coffre au premier lancement et définition du Master Password | ✅ |
| F1 | Déverrouillage, verrouillage manuel (bouton, menu « Mon coffre », Ctrl+L) | ✅ |
| F1 | Verrouillage automatique après une inactivité configurable (1 min à 1 h) | ✅ |
| F2 | Ajout, consultation, modification et suppression (avec confirmation) | ✅ |
| F2 | Champs obligatoires (service, identifiant, mot de passe) et facultatifs (URL, note) | ✅ |
| F3 | Générateur basé sur `RandomNumberGenerator` | ✅ |
| F3 | Choix de la longueur et des jeux de caractères, copie, accès depuis le formulaire | ✅ |
| F4 | Recherche instantanée, insensible à la casse et aux accents | ✅ |
| F4 | Tri alphabétique selon les règles du français (« Écobank » avant « Facebook ») | ✅ |
| F4 | Cinq catégories par défaut et favoris | ✅ |
| F5 | Mots de passe masqués par défaut | ✅ |
| F5 | Affichage temporaire de 10 s, avec masquage manuel possible avant la fin | 🔧 |
| F5 | Copie, puis effacement automatique du presse-papiers après le délai réglé (vérifié à 20 s) | ✅ |
| F6 | Export d'une sauvegarde chiffrée et import | ✅ |
| F6 | Vérification d'intégrité avant restauration | ✅ |

## 3. Interface (§ 6)

| Exigence | Statut |
|---|---|
| Thème sombre et navigation latérale (Tableau de bord, Tous les identifiants, Favoris, Générateur, Paramètres) | ✅ |
| Écran de déverrouillage : logo, champ, bouton et message d'erreur neutre (« Mot de passe incorrect. ») | ✅ |
| Barre supérieure : recherche et bouton d'ajout | ✅ |
| Barre supérieure : profil / coffre (menu « Mon coffre ») | 🔧 |
| Tableau de bord : nombre d'identifiants et derniers ajouts | ✅ |
| Chaque entrée : icône, nom, identifiant, mot de passe masqué et actions | ✅ |
| Aucun mot de passe affiché en clair sans action de l'utilisateur | ✅ |
| Raccourcis Ctrl+F (rechercher), Ctrl+N (ajouter) et Ctrl+L (verrouiller) | ✅ |
| Paramètres : sécurité, verrouillage automatique, presse-papiers et sauvegardes | ✅ |

## 4. Sécurité (§ 8 et § 9)

| Exigence | Mise en œuvre | Statut |
|---|---|---|
| Clé de coffre aléatoire de 256 bits, enrobée par une clé dérivée du Master Password | `VaultKeyProtector` | ✅ |
| AES-256-GCM avec un nonce unique de 96 bits par chiffrement | `AesGcmCipher`, nonce unique sur 10 000 chiffrements | ✅ |
| Argon2id avec sel aléatoire ; paramètres enregistrés et ajustés à la machine | `KdfCalibration` : 64 Mio, 4 voies, et un nombre de passes calibré (5 sur la machine de test, soit environ 0,4 s) | 🔧 |
| Master Password jamais stocké | Seule la clé enrobée est enregistrée | ✅ |
| Contenu sensible chiffré dans une seule colonne, identifiant de l'entrée authentifié | `payload` avec données associées `SafeVault/entry/v1/` + GUID | ✅ |
| Effacement des secrets temporaires | Clés et tampons remis à zéro après usage | ✅ |
| Clés purgées au verrouillage, et presse-papiers vidé | `VaultService.Lock`, `ClipboardService` | ✅ |
| Message d'échec générique et limitation du rythme des tentatives | 3 essais libres, puis un délai de 1 s à 1 min | ✅ |
| Format de sauvegarde (§ 9.2) : en-tête (format, version, KDF, sel, nonce) et contenu en AES-256-GCM | `BackupProtector` | ✅ |
| Altération du fichier du coffre détectée sans rendre le coffre inutilisable | Les entrées illisibles sont ignorées et signalées par un bandeau | 🔧 |
| Avertissement « aucune récupération possible » à la création du coffre | Case à cocher obligatoire | ✅ |

## 5. Stratégie de tests (§ 12)

| Type | Cible | Tests |
|---|---|---|
| Unitaires | Dérivation de clé (vecteur RFC 9106) | `KeyDerivationTests` |
| Unitaires | Chiffrement / déchiffrement aller-retour | `AesGcmCipherTests` |
| Unitaires | Générateur (longueur, jeux de caractères) | `PasswordGeneratorTests` |
| Unitaires | CRUD | `CredentialServiceTests` |
| Sécurité | Nonce jamais réutilisé | `AesGcmCipherTests`, `BackupProtectorTests` |
| Sécurité | Mauvais mot de passe rejeté | `VaultServiceTests`, `BackupProtectorTests` |
| Sécurité | Données altérées rejetées | `StorageSecurityTests`, `BackupProtectorTests` |
| Sécurité | Aucune valeur sensible en clair dans SQLite | `StorageSecurityTests` |
| Sauvegarde | Export puis import identique | `BackupServiceTests` |
| Sauvegarde | Fichier corrompu refusé | `BackupServiceTests` |
| Sauvegarde | Coffre existant intact après échec | `BackupServiceTests` |
| Manuels | Verrouillage automatique | Vérifié : verrouillage après environ 70 s avec un délai d'1 min |
| Manuels | Presse-papiers | Vérifié : vidé après le délai, au verrouillage et à la fermeture |
| Manuels | Masquage | Vérifié |
| Manuels | Parcours complet | Vérifié : installation, création, accès, gestion, verrouillage |

## 6. Écarts corrigés pendant la recette

1. **Le mot de passe affiché ne pouvait pas être masqué avant 10 s.** Le bouton « Masquer » restait désactivé pendant l'affichage temporaire. Il fonctionne désormais à tout moment.
2. **Une entrée altérée dans le fichier fermait l'application** au déverrouillage, ce qui rendait tout le coffre inaccessible, restauration comprise. Désormais :
   - les entrées illisibles sont ignorées ;
   - un bandeau « Données altérées » les signale ;
   - le reste du coffre reste utilisable et une restauration les remplace.
   
   De plus, toute erreur imprévue affiche un message neutre au lieu de fermer l'application.
3. **Les paramètres Argon2id n'étaient pas ajustés à la machine** (§ 9.1). Le nombre de passes est maintenant calibré pour viser environ 0,5 s par dérivation, sans jamais descendre sous 3 passes ni dépasser 10. Les coffres existants restent compatibles, car leurs paramètres sont enregistrés avec eux.
4. **Il manquait l'élément « profil / coffre » dans la barre supérieure** (§ 6.2). Un menu « Mon coffre » a été ajouté : état du coffre, paramètres et sauvegardes, verrouillage.

## 7. Limites connues (détaillées dans [SECURITY.md](../SECURITY.md))

- **Chaînes .NET en mémoire.** Les mots de passe affichés ou saisis transitent par des chaînes .NET, qui ne peuvent pas être effacées de façon fiable. Seuls les tableaux d'octets (clés, tampons) sont remis à zéro.
- **Poste compromis.** Un malware ou un enregistreur de frappe sur le poste reste hors périmètre (§ 9.3).
- **Pas de récupération.** Un Master Password oublié, ou le mot de passe d'une sauvegarde, ne peut pas être récupéré.
- **Paramètres non sauvegardés.** Les paramètres de l'application (délais) ne font pas partie de la sauvegarde.
- **Audit externe.** Le cahier des charges recommande un audit du moteur cryptographique avant toute diffusion publique.
