using SafeVault.Core.Models;
using SafeVault.Security;

namespace SafeVault.Core.Storage;

/// <summary>Entrée telle qu'elle est stockée : seules les métadonnées non sensibles sont en clair.</summary>
/// <param name="Payload">Nom du service, identifiant, mot de passe, URL et note, chiffrés en AES-256-GCM.</param>
public sealed record EncryptedEntry(
    Guid Id,
    int CategoryId,
    bool IsFavorite,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] Payload);

/// <summary>Stockage persistant du coffre. Ne manipule que des données déjà chiffrées.</summary>
public interface IVaultStore
{
    /// <summary>Indique si un coffre a déjà été créé.</summary>
    bool Exists();

    /// <summary>Crée le coffre (schéma, clé enrobée, catégories et paramètres par défaut).</summary>
    void Initialize(VaultKeyEnvelope envelope);

    VaultKeyEnvelope? LoadEnvelope();
    void UpdateEnvelope(VaultKeyEnvelope envelope);

    IReadOnlyList<Category> GetCategories();

    IReadOnlyList<EncryptedEntry> GetEntries();
    void InsertEntry(EncryptedEntry entry);
    void UpdateEntry(EncryptedEntry entry);
    void DeleteEntry(Guid id);

    /// <summary>Remplace toutes les entrées en une seule transaction : en cas d'échec, le coffre reste inchangé.</summary>
    void ReplaceEntries(IReadOnlyList<EncryptedEntry> entries);

    VaultSettings GetSettings();
    void SaveSettings(VaultSettings settings);
}
