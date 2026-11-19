using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SafeVault.Security;

/// <summary>Le fichier n'est pas une sauvegarde SafeVault lisible (format inconnu, version non prise en charge, fichier tronqué).</summary>
public sealed class InvalidBackupFormatException(string message) : Exception(message);

/// <summary>
/// Chiffrement d'un fichier de sauvegarde avec un mot de passe.
/// </summary>
/// <remarks>
/// Format (entiers en little-endian) :
/// <code>
/// "SVBACKUP" (8) | version u16 | mémoire Kio i32 | itérations i32 | parallélisme i32 | sel (16)
/// | nonce (12) | contenu chiffré AES-256-GCM | tag (16)
/// </code>
/// Tout l'en-tête est authentifié (données associées GCM) : une modification des paramètres,
/// du sel ou du contenu est détectée avant toute restauration.
/// </remarks>
public static class BackupProtector
{
    public const ushort CurrentVersion = 1;

    /// <summary>Taille maximale acceptée à l'import (bien au-delà d'un coffre personnel).</summary>
    public const int MaxFileSize = 64 * 1024 * 1024;

    private static ReadOnlySpan<byte> Magic => "SVBACKUP"u8;

    /// <summary>Taille de l'en-tête authentifié, avant le nonce.</summary>
    public const int HeaderSize = 8 + 2 + 3 * 4 + KeyDerivation.SaltSize;

    /// <summary>Taille minimale d'un fichier valide (en-tête, nonce et tag, contenu vide).</summary>
    public const int MinFileSize = HeaderSize + AesGcmCipher.Overhead;

    /// <summary>Chiffre <paramref name="content"/> avec une clé dérivée de <paramref name="password"/> (sel et nonce neufs).</summary>
    public static byte[] Protect(ReadOnlySpan<byte> content, string password, KdfParameters kdf)
    {
        ArgumentNullException.ThrowIfNull(kdf);
        kdf.Validate();

        var header = new byte[HeaderSize];
        Magic.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(8), CurrentVersion);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(10), kdf.MemoryKiB);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(14), kdf.Iterations);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(18), kdf.Parallelism);
        var salt = KeyDerivation.GenerateSalt();
        salt.CopyTo(header.AsSpan(22));

        using var key = KeyDerivation.DeriveKey(password, salt, kdf);
        var encrypted = AesGcmCipher.Encrypt(key, content, header);

        var file = new byte[header.Length + encrypted.Length];
        header.CopyTo(file, 0);
        encrypted.CopyTo(file, header.Length);
        return file;
    }

    /// <summary>Indique si les données commencent par l'identifiant de format SafeVault.</summary>
    public static bool HasBackupSignature(ReadOnlySpan<byte> file) => file.Length >= Magic.Length && file[..Magic.Length].SequenceEqual(Magic);

    /// <summary>Vérifie l'intégrité de la sauvegarde et retourne son contenu déchiffré.</summary>
    /// <exception cref="InvalidBackupFormatException">Fichier non reconnu, tronqué ou de version non prise en charge.</exception>
    /// <exception cref="CryptographicException">Mot de passe incorrect ou fichier altéré (indiscernables par conception).</exception>
    public static byte[] Unprotect(ReadOnlySpan<byte> file, string password)
    {
        if (!HasBackupSignature(file))
        {
            throw new InvalidBackupFormatException("Ce fichier n'est pas une sauvegarde SafeVault.");
        }

        if (file.Length < MinFileSize)
        {
            throw new InvalidBackupFormatException("La sauvegarde est incomplète.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new InvalidBackupFormatException("Le fichier est trop volumineux pour être une sauvegarde SafeVault.");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(file[8..]);
        if (version != CurrentVersion)
        {
            throw new InvalidBackupFormatException($"Version de sauvegarde non prise en charge ({version}).");
        }

        var kdf = new KdfParameters(
            BinaryPrimitives.ReadInt32LittleEndian(file[10..]),
            BinaryPrimitives.ReadInt32LittleEndian(file[14..]),
            BinaryPrimitives.ReadInt32LittleEndian(file[18..]));

        try
        {
            kdf.Validate();
        }
        catch (ArgumentOutOfRangeException)
        {
            // Paramètres hors bornes : fichier altéré ou forgé (évite aussi une dérivation démesurée).
            throw new InvalidBackupFormatException("Les paramètres de la sauvegarde sont invalides.");
        }

        var header = file[..HeaderSize];
        var salt = header[22..];

        using var key = KeyDerivation.DeriveKey(password, salt, kdf);
        return AesGcmCipher.Decrypt(key, file[HeaderSize..], header);
    }
}
