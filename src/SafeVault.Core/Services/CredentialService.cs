using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SafeVault.Core.Models;
using SafeVault.Core.Storage;
using SafeVault.Security;

namespace SafeVault.Core.Services;

/// <summary>
/// Gestion des identifiants. Le contenu sensible de chaque entrée est chiffré dans un seul payload ;
/// la recherche et le tri se font en mémoire, une fois le coffre déverrouillé.
/// </summary>
public sealed class CredentialService
{
    private static readonly CompareInfo Compare = CultureInfo.GetCultureInfo("fr-FR").CompareInfo;
    private const CompareOptions SearchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
    private static readonly StringComparer SortComparer = StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), ignoreCase: true);
    private static readonly byte[] AadLabel = Encoding.ASCII.GetBytes("SafeVault/entry/v1/");

    private readonly VaultService _vault;
    private readonly IVaultStore _store;
    private readonly TimeProvider _time;
    private readonly Lock _sync = new();

    private Dictionary<Guid, Credential>? _cache;
    private int _unreadableCount;

    public CredentialService(VaultService vault, IVaultStore store, TimeProvider? time = null)
    {
        _vault = vault;
        _store = store;
        _time = time ?? TimeProvider.System;

        // Au verrouillage, les identifiants déchiffrés sont retirés de la mémoire.
        _vault.StateChanged += (_, _) =>
        {
            lock (_sync)
            {
                _cache = null;
                _unreadableCount = 0;
            }
        };
    }

    /// <summary>Déclenché après un ajout, une modification, un changement de favori ou une suppression.</summary>
    public event EventHandler? CredentialsChanged;

    public IReadOnlyList<Category> GetCategories() => _store.GetCategories();

    /// <summary>
    /// Nombre d'entrées présentes dans le fichier mais impossibles à déchiffrer (fichier altéré ou endommagé).
    /// Elles sont ignorées : le reste du coffre reste accessible, et une restauration les remplace.
    /// </summary>
    public int UnreadableCount
    {
        get
        {
            lock (_sync)
            {
                _ = Cache;
                return _unreadableCount;
            }
        }
    }

    public int Count
    {
        get { lock (_sync) return Cache.Count; }
    }

    /// <summary>Tous les identifiants, triés par nom de service.</summary>
    public IReadOnlyList<Credential> GetAll() => Search(null);

    /// <summary>
    /// Recherche (sans tenir compte de la casse ni des accents) dans le nom du service,
    /// l'identifiant et l'URL. Résultats triés par nom de service.
    /// </summary>
    public IReadOnlyList<Credential> Search(string? query, int? categoryId = null, bool favoritesOnly = false)
    {
        var text = query?.Trim();

        lock (_sync)
        {
            return Cache.Values
                .Where(c => categoryId is null || c.CategoryId == categoryId)
                .Where(c => !favoritesOnly || c.IsFavorite)
                .Where(c => string.IsNullOrEmpty(text) || Matches(c, text))
                .OrderBy(c => c.ServiceName, SortComparer)
                .ThenBy(c => c.Username, SortComparer)
                .ToList();
        }
    }

    /// <summary>Derniers identifiants ajoutés, du plus récent au plus ancien.</summary>
    public IReadOnlyList<Credential> GetRecent(int count)
    {
        lock (_sync)
        {
            return Cache.Values.OrderByDescending(c => c.CreatedAt).Take(count).ToList();
        }
    }

    public Credential? GetById(Guid id)
    {
        lock (_sync)
        {
            return Cache.GetValueOrDefault(id);
        }
    }

    /// <exception cref="VaultValidationException">Saisie invalide.</exception>
    public Credential Add(CredentialDraft draft)
    {
        ValidateDraft(draft);
        var now = _time.GetUtcNow();

        var credential = new Credential
        {
            Id = Guid.NewGuid(),
            ServiceName = draft.ServiceName.Trim(),
            Username = draft.Username.Trim(),
            Password = draft.Password,
            Url = NullIfBlank(draft.Url),
            Note = NullIfBlank(draft.Note),
            CategoryId = draft.CategoryId,
            IsFavorite = draft.IsFavorite,
            CreatedAt = now,
            UpdatedAt = now,
        };

        lock (_sync)
        {
            var cache = Cache;
            _store.InsertEntry(Encrypt(credential));
            cache[credential.Id] = credential;
        }

        CredentialsChanged?.Invoke(this, EventArgs.Empty);
        return credential;
    }

    /// <exception cref="VaultValidationException">Saisie invalide.</exception>
    /// <exception cref="KeyNotFoundException">Identifiant inconnu.</exception>
    public Credential Update(Guid id, CredentialDraft draft)
    {
        ValidateDraft(draft);

        Credential updated;
        lock (_sync)
        {
            var cache = Cache;
            var existing = cache.GetValueOrDefault(id) ?? throw new KeyNotFoundException("Identifiant introuvable.");

            updated = existing with
            {
                ServiceName = draft.ServiceName.Trim(),
                Username = draft.Username.Trim(),
                Password = draft.Password,
                Url = NullIfBlank(draft.Url),
                Note = NullIfBlank(draft.Note),
                CategoryId = draft.CategoryId,
                IsFavorite = draft.IsFavorite,
                UpdatedAt = _time.GetUtcNow(),
            };

            _store.UpdateEntry(Encrypt(updated));
            cache[id] = updated;
        }

        CredentialsChanged?.Invoke(this, EventArgs.Empty);
        return updated;
    }

    /// <exception cref="KeyNotFoundException">Identifiant inconnu.</exception>
    public Credential SetFavorite(Guid id, bool isFavorite)
    {
        Credential updated;
        lock (_sync)
        {
            var cache = Cache;
            var existing = cache.GetValueOrDefault(id) ?? throw new KeyNotFoundException("Identifiant introuvable.");
            if (existing.IsFavorite == isFavorite)
            {
                return existing;
            }

            updated = existing with { IsFavorite = isFavorite, UpdatedAt = _time.GetUtcNow() };
            _store.UpdateEntry(Encrypt(updated));
            cache[id] = updated;
        }

        CredentialsChanged?.Invoke(this, EventArgs.Empty);
        return updated;
    }

    public void Delete(Guid id)
    {
        lock (_sync)
        {
            var cache = Cache;
            _store.DeleteEntry(id);
            if (!cache.Remove(id))
            {
                return;
            }
        }

        CredentialsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Remplace tout le contenu du coffre (restauration d'une sauvegarde). Les identifiants sont validés
    /// et rechiffrés avec la clé du coffre ouvert, puis écrits en une seule transaction.
    /// </summary>
    /// <exception cref="VaultValidationException">Un identifiant est invalide ou en double : rien n'est modifié.</exception>
    public void ReplaceAll(IReadOnlyList<Credential> credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var categoryIds = _store.GetCategories().Select(c => c.Id).ToHashSet();
        var errors = new List<string>();
        var ids = new HashSet<Guid>();
        foreach (var c in credentials)
        {
            var draft = new CredentialDraft
            {
                ServiceName = c.ServiceName, Username = c.Username, Password = c.Password,
                Url = c.Url, Note = c.Note, CategoryId = c.CategoryId, IsFavorite = c.IsFavorite,
            };
            errors.AddRange(draft.Validate().Select(e => $"{c.ServiceName} : {e}"));
            if (!categoryIds.Contains(c.CategoryId))
            {
                errors.Add($"{c.ServiceName} : catégorie inconnue.");
            }

            if (c.Id == Guid.Empty || !ids.Add(c.Id))
            {
                errors.Add($"{c.ServiceName} : identifiant interne invalide ou en double.");
            }
        }

        if (errors.Count > 0)
        {
            throw new VaultValidationException(errors.Distinct().Take(10).ToList());
        }

        lock (_sync)
        {
            _ = _vault.GetKey(); // lève VaultLockedException si le coffre est verrouillé
            _store.ReplaceEntries(credentials.Select(Encrypt).ToList());
            _cache = credentials.ToDictionary(c => c.Id);
            _unreadableCount = 0;
        }

        CredentialsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Identifiants déchiffrés, chargés au premier accès après le déverrouillage.</summary>
    private Dictionary<Guid, Credential> Cache
    {
        get
        {
            var key = _vault.GetKey(); // lève VaultLockedException si le coffre est verrouillé
            if (_cache is not null)
            {
                return _cache;
            }

            var cache = new Dictionary<Guid, Credential>();
            var unreadable = 0;
            foreach (var entry in _store.GetEntries())
            {
                try
                {
                    var credential = Decrypt(key, entry);
                    cache[credential.Id] = credential;
                }
                catch (VaultDataCorruptedException)
                {
                    // Une entrée altérée ne doit pas rendre tout le coffre inaccessible.
                    unreadable++;
                }
            }

            _unreadableCount = unreadable;
            return _cache = cache;
        }
    }

    private void ValidateDraft(CredentialDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var errors = draft.Validate().ToList();
        if (_store.GetCategories().All(c => c.Id != draft.CategoryId))
        {
            errors.Add("La catégorie sélectionnée n'existe pas.");
        }

        if (errors.Count > 0)
        {
            throw new VaultValidationException(errors);
        }
    }

    private EncryptedEntry Encrypt(Credential credential)
    {
        var payload = new EntryPayload(credential.ServiceName, credential.Username, credential.Password, credential.Url, credential.Note);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload);
        try
        {
            var encrypted = AesGcmCipher.Encrypt(_vault.GetKey(), plaintext, BuildAssociatedData(credential.Id));
            return new EncryptedEntry(credential.Id, credential.CategoryId, credential.IsFavorite, credential.CreatedAt, credential.UpdatedAt, encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static Credential Decrypt(SecretKey key, EncryptedEntry entry)
    {
        byte[] plaintext;
        try
        {
            // L'identifiant de l'entrée est authentifié : un payload déplacé vers une autre ligne est rejeté.
            plaintext = AesGcmCipher.Decrypt(key, entry.Payload, BuildAssociatedData(entry.Id));
        }
        catch (CryptographicException ex)
        {
            throw new VaultDataCorruptedException($"L'entrée {entry.Id} est illisible ou a été modifiée.", ex);
        }

        try
        {
            var payload = JsonSerializer.Deserialize<EntryPayload>(plaintext)
                          ?? throw new VaultDataCorruptedException($"L'entrée {entry.Id} est vide.");

            return new Credential
            {
                Id = entry.Id,
                ServiceName = payload.ServiceName,
                Username = payload.Username,
                Password = payload.Password,
                Url = payload.Url,
                Note = payload.Note,
                CategoryId = entry.CategoryId,
                IsFavorite = entry.IsFavorite,
                CreatedAt = entry.CreatedAt,
                UpdatedAt = entry.UpdatedAt,
            };
        }
        catch (JsonException ex)
        {
            throw new VaultDataCorruptedException($"L'entrée {entry.Id} a un format invalide.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] BuildAssociatedData(Guid id)
    {
        var data = new byte[AadLabel.Length + 16];
        AadLabel.CopyTo(data, 0);
        id.TryWriteBytes(data.AsSpan(AadLabel.Length));
        return data;
    }

    private static bool Matches(Credential c, string text) =>
        Compare.IndexOf(c.ServiceName, text, SearchOptions) >= 0
        || Compare.IndexOf(c.Username, text, SearchOptions) >= 0
        || (c.Url is not null && Compare.IndexOf(c.Url, text, SearchOptions) >= 0);

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Contenu chiffré d'une entrée.</summary>
    private sealed record EntryPayload(string ServiceName, string Username, string Password, string? Url, string? Note);
}
