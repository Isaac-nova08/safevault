using System.Security.Cryptography;

namespace SafeVault.Security;

/// <summary>
/// Chiffrement authentifié AES-256-GCM.
/// Format produit : nonce (12 octets) | texte chiffré | tag (16 octets).
/// </summary>
/// <remarks>
/// Un nonce aléatoire de 96 bits est tiré à chaque chiffrement : avec une même clé,
/// le risque de collision reste négligeable bien au-delà du volume d'un coffre personnel.
/// </remarks>
public static class AesGcmCipher
{
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int Overhead = NonceSize + TagSize;

    /// <summary>Chiffre <paramref name="plaintext"/>. Les données associées sont authentifiées mais non chiffrées.</summary>
    public static byte[] Encrypt(SecretKey key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var output = new byte[Overhead + plaintext.Length];
        var nonce = output.AsSpan(0, NonceSize);
        var ciphertext = output.AsSpan(NonceSize, plaintext.Length);
        var tag = output.AsSpan(NonceSize + plaintext.Length, TagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key.Span, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        return output;
    }

    /// <summary>
    /// Déchiffre et vérifie l'intégrité d'un payload produit par <see cref="Encrypt"/>.
    /// </summary>
    /// <exception cref="CryptographicException">Clé incorrecte, données associées différentes ou payload altéré.</exception>
    public static byte[] Decrypt(SecretKey key, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> associatedData = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (payload.Length < Overhead)
        {
            throw new CryptographicException("Données chiffrées invalides.");
        }

        var nonce = payload[..NonceSize];
        var ciphertext = payload[NonceSize..^TagSize];
        var tag = payload[^TagSize..];

        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key.Span, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
            return plaintext;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw;
        }
    }
}
