using System.Security.Cryptography;
using System.Text.Json;
using SafeVault.Core.Models;
using SafeVault.Security;

namespace SafeVault.Core.Services;

/// <summary>La sauvegarde ne peut pas être lue. <see cref="Exception.Message"/> est destiné à l'utilisateur.</summary>
public sealed class BackupException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Contenu vérifié et déchiffré d'une sauvegarde, prêt à être restauré.</summary>
public sealed record BackupContents(DateTimeOffset ExportedAt, IReadOnlyList<Credential> Credentials);

/// <summary>
/// Export d'une sauvegarde chiffrée et restauration après vérification d'intégrité.
/// </summary>
/// <remarks>
/// La sauvegarde est protégée par un mot de passe choisi à l'export (Argon2id + AES-256-GCM), indépendant
/// de la clé du coffre : elle peut donc être restaurée dans un nouveau coffre, sur un autre poste.
/// Les paramètres de l'application ne sont pas inclus. Les dérivations de clé prennent quelques centaines
/// de millisecondes : appeler ces méthodes hors du thread d'interface.
/// </remarks>
public sealed class BackupService
{
    public const string FileExtension = ".svbackup";
    private const string FormatName = "SafeVault";
    private const int ContentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly CredentialService _credentials;
    private readonly KdfParameters? _kdf;
    private readonly TimeProvider _time;

    public BackupService(CredentialService credentials, KdfParameters? kdf = null, TimeProvider? time = null)
    {
        _credentials = credentials;
        _kdf = kdf;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Paramètres Argon2id des nouvelles dérivations : ceux fournis, sinon calibrés pour cette machine.</summary>
    private KdfParameters Kdf => _kdf ?? KdfCalibration.ForThisMachine;

    /// <summary>Produit le contenu du fichier de sauvegarde de tous les identifiants du coffre ouvert.</summary>
    /// <exception cref="VaultValidationException">Mot de passe de sauvegarde trop faible.</exception>
    /// <exception cref="VaultLockedException">Le coffre est verrouillé.</exception>
    public byte[] Export(string backupPassword)
    {
        var errors = MasterPasswordPolicy.Validate(backupPassword);
        if (errors.Count > 0)
        {
            throw new VaultValidationException(errors);
        }

        var document = new BackupDocument(
            FormatName,
            ContentVersion,
            _time.GetUtcNow(),
            _credentials.GetAll().Select(c => new BackupEntry(
                c.Id, c.ServiceName, c.Username, c.Password, c.Url, c.Note,
                c.CategoryId, c.IsFavorite, c.CreatedAt, c.UpdatedAt)).ToList());

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        try
        {
            return BackupProtector.Protect(plaintext, backupPassword, Kdf);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>
    /// Vérifie l'intégrité de la sauvegarde, la déchiffre et valide son contenu, sans rien modifier.
    /// </summary>
    /// <exception cref="BackupException">Fichier non reconnu, mot de passe incorrect, fichier altéré ou contenu invalide.</exception>
    public BackupContents Open(ReadOnlySpan<byte> file, string backupPassword)
    {
        if (string.IsNullOrEmpty(backupPassword))
        {
            throw new BackupException("Saisissez le mot de passe de la sauvegarde.");
        }

        byte[] plaintext;
        try
        {
            plaintext = BackupProtector.Unprotect(file, backupPassword);
        }
        catch (InvalidBackupFormatException ex)
        {
            throw new BackupException(ex.Message, ex);
        }
        catch (CryptographicException ex)
        {
            // Le tag GCM ne permet pas de distinguer les deux cas, et c'est voulu.
            throw new BackupException("Mot de passe incorrect ou fichier de sauvegarde altéré.", ex);
        }

        try
        {
            var document = JsonSerializer.Deserialize<BackupDocument>(plaintext, JsonOptions);
            if (document is not { Format: FormatName, Version: ContentVersion, Entries: not null })
            {
                throw new BackupException("Le contenu de la sauvegarde n'est pas reconnu.");
            }

            var credentials = document.Entries.Select(e => new Credential
            {
                Id = e.Id,
                ServiceName = e.ServiceName ?? "",
                Username = e.Username ?? "",
                Password = e.Password ?? "",
                Url = e.Url,
                Note = e.Note,
                CategoryId = e.CategoryId,
                IsFavorite = e.IsFavorite,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
            }).ToList();

            return new BackupContents(document.ExportedAt, credentials);
        }
        catch (JsonException ex)
        {
            throw new BackupException("Le contenu de la sauvegarde est illisible.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>
    /// Remplace le contenu du coffre ouvert par celui de la sauvegarde, en une seule transaction.
    /// En cas d'échec, le coffre existant reste intact.
    /// </summary>
    /// <exception cref="BackupException">Contenu invalide (rien n'est modifié).</exception>
    /// <exception cref="VaultLockedException">Le coffre est verrouillé.</exception>
    public void Restore(BackupContents contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        try
        {
            _credentials.ReplaceAll(contents.Credentials);
        }
        catch (VaultValidationException ex)
        {
            throw new BackupException("La sauvegarde contient des données invalides : " + string.Join(" ; ", ex.Errors), ex);
        }
    }

    private sealed record BackupDocument(string Format, int Version, DateTimeOffset ExportedAt, List<BackupEntry>? Entries);

    private sealed record BackupEntry(
        Guid Id,
        string? ServiceName,
        string? Username,
        string? Password,
        string? Url,
        string? Note,
        int CategoryId,
        bool IsFavorite,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
