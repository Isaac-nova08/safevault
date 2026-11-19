using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace SafeVault.Security;

/// <summary>
/// Gestion de la clé de coffre :
/// une clé aléatoire de 256 bits chiffre les entrées, et elle est elle-même chiffrée (« enrobée »)
/// par une clé dérivée du Master Password avec Argon2id.
/// </summary>
/// <remarks>
/// Le Master Password n'est jamais stocké, même sous forme de hash : un déchiffrement réussi
/// de la clé enrobée (tag GCM valide) prouve que le mot de passe est correct.
/// Changer de Master Password revient à ré-enrober la même clé, sans rechiffrer les entrées.
/// </remarks>
public static class VaultKeyProtector
{
    public const int CurrentVersion = 1;

    private static readonly byte[] AadLabel = Encoding.ASCII.GetBytes("SafeVault/vault-key");

    /// <summary>Crée la clé d'un nouveau coffre et son enveloppe protégée par le Master Password.</summary>
    /// <returns>L'enveloppe à stocker et la clé de coffre déverrouillée (à libérer par l'appelant).</returns>
    public static (VaultKeyEnvelope Envelope, SecretKey VaultKey) CreateVault(string masterPassword, KdfParameters? kdf = null)
    {
        var vaultKey = SecretKey.Generate();
        try
        {
            var envelope = Wrap(vaultKey, masterPassword, kdf ?? KdfParameters.Default);
            return (envelope, vaultKey);
        }
        catch
        {
            vaultKey.Dispose();
            throw;
        }
    }

    /// <summary>Tente de déverrouiller le coffre. Retourne <c>false</c> si le mot de passe est incorrect ou l'enveloppe altérée.</summary>
    /// <exception cref="NotSupportedException">Version d'enveloppe inconnue.</exception>
    public static bool TryUnlock(string masterPassword, VaultKeyEnvelope envelope, [NotNullWhen(true)] out SecretKey? vaultKey)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.Version != CurrentVersion)
        {
            throw new NotSupportedException($"Version de coffre non prise en charge : {envelope.Version}.");
        }

        vaultKey = null;

        using var keyEncryptionKey = KeyDerivation.DeriveKey(masterPassword, envelope.Salt, envelope.Kdf);

        byte[] rawKey;
        try
        {
            rawKey = AesGcmCipher.Decrypt(keyEncryptionKey, envelope.WrappedKey, BuildAssociatedData(envelope.Version, envelope.Salt, envelope.Kdf));
        }
        catch (CryptographicException)
        {
            return false;
        }

        if (rawKey.Length != SecretKey.SizeInBytes)
        {
            CryptographicOperations.ZeroMemory(rawKey);
            return false;
        }

        vaultKey = SecretKey.TakeOwnership(rawKey);
        return true;
    }

    /// <summary>
    /// Enrobe la clé de coffre avec un nouveau Master Password (nouveau sel).
    /// Sert à la création du coffre et au changement de Master Password.
    /// </summary>
    public static VaultKeyEnvelope Wrap(SecretKey vaultKey, string masterPassword, KdfParameters? kdf = null)
    {
        ArgumentNullException.ThrowIfNull(vaultKey);
        kdf ??= KdfParameters.Default;

        var salt = KeyDerivation.GenerateSalt();
        using var keyEncryptionKey = KeyDerivation.DeriveKey(masterPassword, salt, kdf);

        var wrappedKey = AesGcmCipher.Encrypt(keyEncryptionKey, vaultKey.Span, BuildAssociatedData(CurrentVersion, salt, kdf));
        return new VaultKeyEnvelope(CurrentVersion, salt, kdf, wrappedKey);
    }

    /// <summary>
    /// Lie la clé enrobée à la version, au sel et aux paramètres KDF :
    /// toute modification de ces métadonnées dans la base fait échouer le déverrouillage.
    /// </summary>
    private static byte[] BuildAssociatedData(int version, byte[] salt, KdfParameters kdf)
    {
        var data = new byte[AadLabel.Length + 4 * sizeof(int) + salt.Length];
        var span = data.AsSpan();

        AadLabel.CopyTo(span);
        span = span[AadLabel.Length..];
        BinaryPrimitives.WriteInt32LittleEndian(span, version);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], kdf.MemoryKiB);
        BinaryPrimitives.WriteInt32LittleEndian(span[8..], kdf.Iterations);
        BinaryPrimitives.WriteInt32LittleEndian(span[12..], kdf.Parallelism);
        salt.CopyTo(span[16..]);

        return data;
    }
}
