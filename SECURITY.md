# Sécurité de SafeVault

Ce document décrit les choix cryptographiques de SafeVault 1.0, le format des données, le modèle de menaces et les limites connues.

> SafeVault n'a pas fait l'objet d'un **audit de sécurité externe**. Une relecture indépendante du moteur cryptographique est recommandée avant de lui confier des données sensibles à grande échelle.

## 1. Principes

- **Local uniquement.** Aucun serveur, aucun compte, aucune connexion réseau.
- **Rien de sensible en clair sur le disque.** Le nom du service, l'identifiant, le mot de passe, l'URL et la note sont chiffrés. Seules quelques métadonnées restent en clair : catégorie, favori, dates et identifiant interne.
- **Le Master Password n'est jamais stocké**, ni en clair ni sous forme de hash.
- **Primitives standard.** Le chiffrement utilise l'API .NET `AesGcm` ; la dérivation de clé utilise Argon2id via [Konscious.Security.Cryptography](https://github.com/kmaragon/Konscious.Security.Cryptography). Aucun algorithme maison.

## 2. Hiérarchie des clés

```
Master Password ──Argon2id(sel, paramètres)──► clé d'enrobage (256 bits)
                                                     │ AES-256-GCM
                                                     ▼
                              clé de coffre aléatoire (256 bits), stockée enrobée
                                                     │ AES-256-GCM
                                                     ▼
                                       payload chiffré de chaque entrée
```

- **Clé de coffre.** 256 bits tirés par `RandomNumberGenerator` à la création du coffre. Elle chiffre toutes les entrées.
- **Clé d'enrobage.** Dérivée du Master Password avec Argon2id. Elle ne sert qu'à chiffrer la clé de coffre.
- **Vérification du Master Password.** Un déchiffrement réussi de la clé enrobée (tag GCM valide) prouve que le mot de passe est correct. Aucun hash n'est conservé.
- **Changement de Master Password.** La même clé de coffre est enrobée de nouveau, avec un nouveau sel. Les entrées ne sont pas rechiffrées.

## 3. Paramètres cryptographiques

| Élément | Choix |
|---|---|
| Chiffrement | AES-256-GCM, nonce aléatoire de 96 bits par opération, tag de 128 bits |
| Dérivation de clé | Argon2id, 64 Mio de mémoire, 4 voies, sel aléatoire de 128 bits |
| Nombre de passes Argon2id | Calibré sur la machine pour viser environ 0,5 s, entre 3 et 10 passes. Les paramètres sont stockés avec le coffre. |
| Normalisation | Le Master Password est normalisé en Unicode NFC avant la dérivation |
| Aléa | `System.Security.Cryptography.RandomNumberGenerator` (clés, sels, nonces, générateur) |

Ces paramètres dépassent la recommandation OWASP pour Argon2id (19 Mio, 2 passes).

**Validation des paramètres lus.** Les paramètres provenant d'un fichier sont bornés : 1 Gio de mémoire au plus, 64 passes, 16 voies. Un fichier forgé ne peut donc pas imposer une dérivation démesurée.

## 4. Formats

### Clé de coffre enrobée (table `vault_meta`)

`wrapped_vault_key` = nonce (12) ‖ clé chiffrée (32) ‖ tag (16)

Données associées authentifiées : `"SafeVault/vault-key"` ‖ version ‖ mémoire ‖ passes ‖ voies ‖ sel. Toute modification de la version, des paramètres ou du sel dans la base fait échouer le déverrouillage.

### Entrée (table `entries`)

`payload` = nonce (12) ‖ JSON chiffré {service, identifiant, mot de passe, URL, note} ‖ tag (16)

Données associées : `"SafeVault/entry/v1/"` ‖ GUID de l'entrée. Un payload copié d'une ligne vers une autre est détecté.

### Sauvegarde (`.svbackup`)

| Champ | Taille | Description |
|---|---|---|
| `SVBACKUP` | 8 | Identifiant de format |
| version | 2 | `1` |
| mémoire, passes, voies | 3 × 4 | Paramètres Argon2id |
| sel | 16 | Aléatoire, nouveau à chaque export |
| nonce | 12 | Aléatoire |
| contenu chiffré | n | JSON des identifiants, AES-256-GCM |
| tag | 16 | Intégrité |

- **Tout l'en-tête est authentifié** comme données associées.
- **Un mot de passe propre à la sauvegarde.** Il est choisi à l'export et soumis à la même politique que le Master Password. La sauvegarde peut ainsi être restaurée dans un autre coffre ou sur un autre poste.
- **Restauration en deux temps :**
  1. le fichier est entièrement vérifié et déchiffré, puis son contenu validé ;
  2. après confirmation, les entrées sont rechiffrées avec la clé du coffre ouvert et écrites en **une seule transaction SQLite**.
- **En cas d'échec, le coffre existant reste intact.**
- Les entiers de l'en-tête sont stockés en little-endian.

## 5. Mesures de protection

| Mesure | Détail |
|---|---|
| Politique de Master Password | Au moins 10 caractères et 3 types de caractères, ou une phrase de passe de 16 caractères ou plus. Les répétitions sont refusées. |
| Tentatives de déverrouillage | Message neutre (« Mot de passe incorrect. »). Après 3 échecs, un délai croissant s'applique (1 s, 2 s, 4 s… jusqu'à 1 min). |
| Verrouillage | Manuel (`Ctrl+L`) ou automatique après inactivité (1 min à 1 h, 5 min par défaut). La clé de coffre est effacée et les identifiants déchiffrés sont retirés de la mémoire. |
| Mémoire | Les clés et les tampons temporaires sont remis à zéro après usage (`CryptographicOperations.ZeroMemory`). |
| Presse-papiers | Exclu de l'historique et de la synchronisation Windows. Effacé après le délai réglé (20 s par défaut), au verrouillage et à la fermeture, seulement si son contenu n'a pas changé entre-temps. |
| Affichage | Mots de passe masqués par défaut. L'affichage temporaire dure 10 s. |
| Base SQLite | `secure_delete` activé : les données supprimées ou modifiées sont écrasées. |
| Intégrité | Une entrée altérée est détectée (tag GCM), ignorée et signalée, sans bloquer le reste du coffre. |

## 6. Modèle de menaces

| Menace | Protection | Limite |
|---|---|---|
| Vol du fichier du coffre ou d'une sauvegarde | AES-256-GCM et Argon2id | La résistance dépend de la force du mot de passe choisi |
| Accès à un poste resté déverrouillé | Verrouillage automatique | Protège peu si l'attaquant agit pendant le délai |
| Fuite par le presse-papiers | Effacement automatique, exclusion de l'historique | Fenêtre d'exposition courte mais réelle |
| Altération du coffre ou d'une sauvegarde | Tags GCM et données associées | Détection uniquement : les données altérées sont perdues, d'où l'intérêt des sauvegardes |
| Malware, enregistreur de frappe ou accès administrateur au poste | Hors périmètre | Aucun gestionnaire local ne protège d'un poste compromis |

## 7. Limites connues

- **Chaînes .NET en mémoire.** Les mots de passe saisis ou affichés transitent par des chaînes .NET et par les contrôles WinUI. Ces chaînes sont immuables et gérées par le ramasse-miettes : elles ne peuvent pas être effacées de façon fiable et peuvent rester en mémoire, ou dans un fichier d'échange ou de vidage, jusqu'à leur collecte. Seuls les tableaux d'octets (clés, tampons) sont remis à zéro.
- **Coffre ouvert.** Tant que le coffre est déverrouillé, les identifiants déchiffrés sont en mémoire.
- **Aucune récupération.** Un Master Password oublié, ou le mot de passe d'une sauvegarde, ne peut pas être récupéré. Les données sont alors définitivement perdues. C'est volontaire : il n'y a pas de porte dérobée.
- **Métadonnées en clair.** La catégorie, l'état favori, les dates et le nombre d'entrées sont visibles par quiconque lit le fichier.
- **Limitation des tentatives en mémoire.** Le délai entre tentatives est réinitialisé au redémarrage de l'application. Il freine les essais dans l'interface, pas une attaque hors ligne sur le fichier ; c'est Argon2id qui protège contre cette dernière.
- **Signature du paquet.** Le paquet MSIX est signé avec un certificat de test auto-signé.
- **Paramètres non sauvegardés.** Les paramètres de l'application (délais) ne sont pas inclus dans les sauvegardes.

## 8. Signaler une vulnérabilité

Merci de **ne pas** publier de vulnérabilité dans une issue publique. Utilisez la fonction **« Report a vulnerability »** de l'onglet *Security* du dépôt GitHub (signalement privé), en décrivant le problème et les étapes pour le reproduire.
